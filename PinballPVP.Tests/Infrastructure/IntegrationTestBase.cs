using System.Net.Http.Headers;
using System.Net.Http.Json;
using PinballPVP.Api.Dtos;

namespace PinballPVP.Tests.Infrastructure;

[Collection("Integration")]
public abstract class IntegrationTestBase(PinballApiFactory factory) : IAsyncLifetime
{
    protected readonly PinballApiFactory Factory = factory;
    protected readonly HttpClient Client = factory.CreateClient();

    // xUnit creates a new instance per test method, so InitializeAsync runs before each test.
    public virtual async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        Factory.EmailService.Reset();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a new user and logs in, returning the user id and token pair.
    /// All parameters auto-generate unique values when omitted.
    /// </summary>
    protected async Task<(int UserId, string Token, string RefreshToken)> RegisterAndLoginAsync(
        string? username = null,
        string? email    = null,
        string? nickname = null,
        string password  = "Password123!")
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        username ??= $"user_{uid}";
        email    ??= $"{uid}@test.com";
        nickname ??= $"Nick_{uid}";

        var reg = await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto(username, nickname, email, password));
        reg.EnsureSuccessStatusCode();
        var user = await reg.Content.ReadFromJsonAsync<UserResponseDto>();

        var login = await Client.PostAsJsonAsync("/api/v1/auth", new LoginDto(username, password));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponseDto>();

        return (user!.Id, tokens!.Token, tokens.RefreshToken);
    }

    protected void Authorize(string token) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    protected void Deauthorize() =>
        Client.DefaultRequestHeaders.Authorization = null;
}
