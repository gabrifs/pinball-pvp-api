using System.Net;
using System.Net.Http.Json;
using PinballPVP.Api.Dtos;
using PinballPVP.Tests.Infrastructure;

namespace PinballPVP.Tests.Integration;

public class AuthTests(PinballApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokenPair()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto($"user_{uid}", $"Nick_{uid}", $"{uid}@test.com", "Password123!"));

        var resp = await Client.PostAsJsonAsync("/api/v1/auth",
            new LoginDto($"user_{uid}", "Password123!"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotEmpty(body!.Token);
        Assert.NotEmpty(body.RefreshToken);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto($"user_{uid}", $"Nick_{uid}", $"{uid}@test.com", "Password123!"));

        var resp = await Client.PostAsJsonAsync("/api/v1/auth",
            new LoginDto($"user_{uid}", "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_NonexistentUser_Returns401()
    {
        var resp = await Client.PostAsJsonAsync("/api/v1/auth",
            new LoginDto("nobody_here", "Password123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ExistingUser_Returns200AndSendsCode()
    {
        var (userId, _, _) = await RegisterAndLoginAsync();

        var resp = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new ForgotPasswordDto(userId));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(Factory.EmailService.LastRecoveryCode);
    }

    [Fact]
    public async Task ForgotPassword_NonexistentUser_Returns200WithoutRevealingExistence()
    {
        var resp = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new ForgotPasswordDto(99999));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Null(Factory.EmailService.LastRecoveryCode); // No email sent
    }

    [Fact]
    public async Task ResetPassword_ValidCode_AllowsLoginWithNewPassword()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var username = $"user_{uid}";
        var reg = await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto(username, $"Nick_{uid}", $"{uid}@test.com", "Password123!"));
        var user = await reg.Content.ReadFromJsonAsync<UserResponseDto>();

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordDto(user!.Id));
        var code = Factory.EmailService.LastRecoveryCode!;

        var reset = await Client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordDto(user.Id, code, "NewPassword456!"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await Client.PostAsJsonAsync("/api/v1/auth", new LoginDto(username, "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await Client.PostAsJsonAsync("/api/v1/auth", new LoginDto(username, "NewPassword456!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidCode_Returns400()
    {
        var (userId, _, _) = await RegisterAndLoginAsync();

        var resp = await Client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordDto(userId, "INVALID1", "NewPassword456!"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_CodeIsOneTimeUse_SecondAttemptFails()
    {
        var (userId, _, _) = await RegisterAndLoginAsync();
        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordDto(userId));
        var code = Factory.EmailService.LastRecoveryCode!;

        var first = await Client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordDto(userId, code, "NewPassword456!"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordDto(userId, code, "AnotherPassword789!"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewRotatedPair()
    {
        var (_, _, refreshToken) = await RegisterAndLoginAsync();

        var resp = await Client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequestDto(refreshToken));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotEmpty(body!.Token);
        Assert.NotEqual(refreshToken, body.RefreshToken); // Token was rotated
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var resp = await Client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequestDto("invalid-refresh-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var (_, token, refreshToken) = await RegisterAndLoginAsync();
        Authorize(token);

        var logout = await Client.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshRequestDto(refreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequestDto(refreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
