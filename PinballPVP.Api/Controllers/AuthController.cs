using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Services.Auth;
using PinballPVP.Api.Services.Password;
using PinballPVP.Api.Services.RateLimiting;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    PinballPVPContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService) : ControllerBase
{
    private readonly PinballPVPContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;

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
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _refreshTokenService.RevokeAllForUserAsync(user.Id);
        var accessToken = _jwtTokenService.GenerateToken(user);
        var refreshToken = await _refreshTokenService.CreateAsync(user.Id);
        await transaction.CommitAsync();

        return Ok(new LoginResponseDto(accessToken, refreshToken));
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
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _refreshTokenService.RevokeAsync(existing);
        var newAccessToken = _jwtTokenService.GenerateToken(user);
        var newRefreshToken = await _refreshTokenService.CreateAsync(user.Id);
        await transaction.CommitAsync();

        return Ok(new LoginResponseDto(newAccessToken, newRefreshToken));
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
}
