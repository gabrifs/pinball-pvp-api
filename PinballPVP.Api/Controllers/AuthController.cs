using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Services.Auth;
using PinballPVP.Api.Services.RateLimiting;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    // POST /api/auth
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var result = await authService.LoginAsync(dto);

        return result.Error switch
        {
            LoginError.None => Ok(result.Tokens),
            _                => Unauthorized("Invalid username or password")
        };
    }

    // POST /api/auth/refresh
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponseDto>> Refresh(RefreshRequestDto dto)
    {
        var result = await authService.RefreshAsync(dto.RefreshToken);

        return result.Error switch
        {
            RefreshError.None         => Ok(result.Tokens),
            RefreshError.InvalidToken => Unauthorized("Invalid or expired refresh token"),
            _                          => Unauthorized("User not found")
        };
    }

    // POST /api/auth/logout
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto dto)
    {
        var error = await authService.LogoutAsync(User.GetUserId(), dto.RefreshToken);

        return error switch
        {
            LogoutError.None => NoContent(),
            _                 => Forbid()
        };
    }

    // POST /api/auth/forgot-password
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        await authService.ForgotPasswordAsync(dto);

        return Ok(); // Don't reveal whether the user exists
    }

    // POST /api/auth/reset-password
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var error = await authService.ResetPasswordAsync(dto);

        return error switch
        {
            ResetPasswordError.None => Ok(),
            _                        => BadRequest("Invalid or expired recovery code")
        };
    }
}
