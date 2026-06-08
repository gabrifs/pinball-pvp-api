using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PinballPVP.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token is missing the 'sub' claim");

        return int.Parse(subject);
    }
}
