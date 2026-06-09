using PinballPVP.Api.Models;

namespace PinballPVP.Api.Services.Auth;

public interface IRefreshTokenService
{
    Task<string> CreateAsync(int userId, CancellationToken ct = default);
    Task<RefreshToken?> ValidateAsync(string rawToken, CancellationToken ct = default);
    Task RevokeAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllForUserAsync(int userId, CancellationToken ct = default);
}
