using PinballPVP.Api.Models;

namespace PinballPVP.Api.Services.Auth;

public interface IJwtTokenService
{
    public string GenerateToken(User user);
}
