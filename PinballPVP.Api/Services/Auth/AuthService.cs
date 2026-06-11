using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Models;
using PinballPVP.Api.Services.Email;
using PinballPVP.Api.Services.Password;

namespace PinballPVP.Api.Services.Auth;

public class AuthService(
    PinballPVPContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IEmailService emailService,
    IConfiguration configuration) : IAuthService
{
    private static string HashCode(string rawCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode))).ToLowerInvariant();

    public async Task<LoginResult> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(user => user.Username == dto.Username)
            .FirstOrDefaultAsync(ct);

        if (user == null || !passwordHasher.Verify(user.PasswordHash, dto.Password))
            return LoginResult.Failure(LoginError.InvalidCredentials);

        // Single-session policy: revoke any existing tokens before issuing a new one.
        // This also cleans up dangling tokens from crashed/disconnected sessions.
        // ExecuteAsync is required by NpgsqlRetryingExecutionStrategy when using explicit transactions.
        var strategy = context.Database.CreateExecutionStrategy();
        var tokens = await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await context.Database.BeginTransactionAsync(ct);
            await refreshTokenService.RevokeAllForUserAsync(user.Id, ct);
            var accessToken = jwtTokenService.GenerateToken(user);
            var refreshToken = await refreshTokenService.CreateAsync(user.Id, ct);
            await transaction.CommitAsync(ct);
            return new LoginResponseDto(accessToken, refreshToken);
        });

        return LoginResult.Success(tokens);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await refreshTokenService.ValidateAsync(refreshToken, ct);
        if (existing is null)
            return RefreshResult.Failure(RefreshError.InvalidToken);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);

        if (user is null)
            return RefreshResult.Failure(RefreshError.UserNotFound);

        // Rotate tokens atomically: revoke old, issue new
        // ExecuteAsync is required by NpgsqlRetryingExecutionStrategy when using explicit transactions.
        var strategy = context.Database.CreateExecutionStrategy();
        var tokens = await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await context.Database.BeginTransactionAsync(ct);
            await refreshTokenService.RevokeAsync(existing, ct);
            var newAccessToken = jwtTokenService.GenerateToken(user);
            var newRefreshToken = await refreshTokenService.CreateAsync(user.Id, ct);
            await transaction.CommitAsync(ct);
            return new LoginResponseDto(newAccessToken, newRefreshToken);
        });

        return RefreshResult.Success(tokens);
    }

    public async Task<LogoutError> LogoutAsync(int userId, string refreshToken, CancellationToken ct = default)
    {
        var existing = await refreshTokenService.ValidateAsync(refreshToken, ct);
        if (existing is null)
            return LogoutError.None; // Already revoked or expired — idempotent

        if (existing.UserId != userId)
            return LogoutError.Forbidden;

        await refreshTokenService.RevokeAsync(existing, ct);
        return LogoutError.None;
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == dto.UserId)
            .Select(u => new { u.Email, u.Nickname })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return; // Don't reveal whether the user exists

        // Invalidate any existing active codes before issuing a new one
        await context.PasswordRecoveryCodes
            .Where(rc => rc.UserId == dto.UserId && !rc.Used && rc.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(rc => rc.Used, true), ct);

        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)); // 8 uppercase hex chars
        var expirationMinutes = configuration.GetValue<int>("PasswordRecovery:ExpirationMinutes", 15);

        context.PasswordRecoveryCodes.Add(new PasswordRecoveryCode
        {
            UserId    = dto.UserId,
            CodeHash  = HashCode(rawCode),
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        });

        await context.SaveChangesAsync(ct);

        await emailService.SendPasswordRecoveryAsync(user.Email, user.Nickname, rawCode, expirationMinutes, ct);
    }

    public async Task<ResetPasswordError> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        var codeHash = HashCode(dto.RecoveryCode);

        var recoveryCode = await context.PasswordRecoveryCodes
            .FirstOrDefaultAsync(rc =>
                rc.UserId == dto.UserId &&
                rc.CodeHash == codeHash &&
                !rc.Used &&
                rc.ExpiresAt > DateTime.UtcNow, ct);

        if (recoveryCode is null)
            return ResetPasswordError.InvalidOrExpiredCode;

        var user = await context.Users.FindAsync([dto.UserId], ct);
        if (user is null)
            return ResetPasswordError.InvalidOrExpiredCode;

        recoveryCode.Used = true;
        user.PasswordHash = passwordHasher.Hash(dto.NewPassword);

        await context.SaveChangesAsync(ct);

        // A password reset is often prompted by a compromised credential, so revoke any
        // existing refresh tokens — mirrors the single-session policy enforced on Login.
        await refreshTokenService.RevokeAllForUserAsync(user.Id, ct);

        return ResetPasswordError.None;
    }
}
