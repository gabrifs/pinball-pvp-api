using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Services.Auth;

public class RefreshTokenService(PinballPVPContext context, IConfiguration configuration) : IRefreshTokenService
{
    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    public async Task<string> CreateAsync(int userId, CancellationToken ct = default)
    {
        var expirationDays = configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 30);
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        });

        await context.SaveChangesAsync(ct);

        return rawToken;
    }

    public async Task<RefreshToken?> ValidateAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
        var token = await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        return token?.IsActive == true ? token : null;
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken ct = default)
    {
        token.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
