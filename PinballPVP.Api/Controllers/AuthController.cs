using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;
using PinballPVP.Api.Services.Auth;
using PinballPVP.Api.Services.Email;
using PinballPVP.Api.Services.Password;
using PinballPVP.Api.Services.RateLimiting;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController(
    PinballPVPContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IEmailService emailService,
    IConfiguration configuration) : ControllerBase
{
    private readonly PinballPVPContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly IEmailService _emailService = emailService;
    private readonly IConfiguration _configuration = configuration;

    private static string HashCode(string rawCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode))).ToLowerInvariant();

    // POST /api/auth
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(user => user.Username == dto.Username)
            .FirstOrDefaultAsync();

        if (user == null || !_passwordHasher.Verify(user.PasswordHash, dto.Password))
            return Unauthorized("Invalid username or password");

        // Single-session policy: revoke any existing tokens before issuing a new one.
        // This also cleans up dangling tokens from crashed/disconnected sessions.
        // ExecuteAsync is required by NpgsqlRetryingExecutionStrategy when using explicit transactions.
        var strategy = _context.Database.CreateExecutionStrategy();
        var tokens = await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            await _refreshTokenService.RevokeAllForUserAsync(user.Id);
            var accessToken = _jwtTokenService.GenerateToken(user);
            var refreshToken = await _refreshTokenService.CreateAsync(user.Id);
            await transaction.CommitAsync();
            return new LoginResponseDto(accessToken, refreshToken);
        });

        return Ok(tokens);
    }

    // POST /api/auth/refresh
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponseDto>> Refresh(RefreshRequestDto dto)
    {
        var existing = await _refreshTokenService.ValidateAsync(dto.RefreshToken);
        if (existing is null)
            return Unauthorized("Invalid or expired refresh token");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == existing.UserId);

        if (user is null)
            return Unauthorized("User not found");

        // Rotate tokens atomically: revoke old, issue new
        // ExecuteAsync is required by NpgsqlRetryingExecutionStrategy when using explicit transactions.
        var strategy = _context.Database.CreateExecutionStrategy();
        var tokens = await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            await _refreshTokenService.RevokeAsync(existing);
            var newAccessToken = _jwtTokenService.GenerateToken(user);
            var newRefreshToken = await _refreshTokenService.CreateAsync(user.Id);
            await transaction.CommitAsync();
            return new LoginResponseDto(newAccessToken, newRefreshToken);
        });

        return Ok(tokens);
    }

    // POST /api/auth/logout
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto dto)
    {
        var existing = await _refreshTokenService.ValidateAsync(dto.RefreshToken);
        if (existing is null)
            return NoContent(); // Already revoked or expired — idempotent

        if (existing.UserId != User.GetUserId())
            return Forbid();

        await _refreshTokenService.RevokeAsync(existing);
        return NoContent();
    }

    // POST /api/auth/forgot-password
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == dto.UserId)
            .Select(u => new { u.Email, u.Nickname })
            .FirstOrDefaultAsync();

        if (user is null)
            return Ok(); // Don't reveal whether the user exists

        // Invalidate any existing active codes before issuing a new one
        await _context.PasswordRecoveryCodes
            .Where(rc => rc.UserId == dto.UserId && !rc.Used && rc.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(rc => rc.Used, true));

        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)); // 8 uppercase hex chars
        var expirationMinutes = _configuration.GetValue<int>("PasswordRecovery:ExpirationMinutes", 15);

        _context.PasswordRecoveryCodes.Add(new PasswordRecoveryCode
        {
            UserId    = dto.UserId,
            CodeHash  = HashCode(rawCode),
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        });

        await _context.SaveChangesAsync();

        await _emailService.SendPasswordRecoveryAsync(user.Email, user.Nickname, rawCode, expirationMinutes);

        return Ok();
    }

    // POST /api/auth/reset-password
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var codeHash = HashCode(dto.RecoveryCode);

        var recoveryCode = await _context.PasswordRecoveryCodes
            .FirstOrDefaultAsync(rc =>
                rc.UserId == dto.UserId &&
                rc.CodeHash == codeHash &&
                !rc.Used &&
                rc.ExpiresAt > DateTime.UtcNow);

        if (recoveryCode is null)
            return BadRequest("Invalid or expired recovery code");

        var user = await _context.Users.FindAsync(dto.UserId);
        if (user is null)
            return BadRequest("Invalid or expired recovery code");

        recoveryCode.Used = true;
        user.PasswordHash = _passwordHasher.Hash(dto.NewPassword);

        await _context.SaveChangesAsync();

        // A password reset is often prompted by a compromised credential, so revoke any
        // existing refresh tokens — mirrors the single-session policy enforced on Login.
        await _refreshTokenService.RevokeAllForUserAsync(user.Id);

        return Ok();
    }
}
