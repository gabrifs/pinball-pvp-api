using System.Net;
using System.Net.Http.Json;
using PinballPVP.Api.Dtos;
using PinballPVP.Tests.Infrastructure;

namespace PinballPVP.Tests.Integration;

public class UsersTests(PinballApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Register_ValidData_Returns201WithUser()
    {
        var resp = await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("alice", "Alice", "alice@test.com", "Password123!"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("Alice", body!.Nickname);
        Assert.True(body.Id > 0);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("alice", "Alice", "alice@test.com", "Password123!"));

        var resp = await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("alice", "AliceTwo", "alice2@test.com", "Password123!"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("alice", "Alice", "shared@test.com", "Password123!"));

        var resp = await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("bob", "Bob", "shared@test.com", "Password123!"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateNickname_Returns400()
    {
        await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("alice", "SharedNick", "alice@test.com", "Password123!"));

        var resp = await Client.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto("bob", "SharedNick", "bob@test.com", "Password123!"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetUser_ExistingUser_Returns200()
    {
        var (userId, _, _) = await RegisterAndLoginAsync();

        var resp = await Client.GetAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal(userId, body!.Id);
    }

    [Fact]
    public async Task GetUser_NonexistentUser_Returns404()
    {
        var resp = await Client.GetAsync("/api/v1/users/99999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateNickname_AsOwner_Returns200WithUpdatedNickname()
    {
        var (userId, token, _) = await RegisterAndLoginAsync();
        Authorize(token);

        var resp = await Client.PatchAsJsonAsync($"/api/v1/users/{userId}/nickname",
            new UpdateNicknameDto("BrandNewNick"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("BrandNewNick", body!.Nickname);
    }

    [Fact]
    public async Task UpdateNickname_Unauthenticated_Returns401()
    {
        var (userId, _, _) = await RegisterAndLoginAsync();

        var resp = await Client.PatchAsJsonAsync($"/api/v1/users/{userId}/nickname",
            new UpdateNicknameDto("BrandNewNick"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateNickname_AsDifferentUser_Returns403()
    {
        var (aliceId, _, _) = await RegisterAndLoginAsync();
        var (_, bobToken, _) = await RegisterAndLoginAsync();
        Authorize(bobToken);

        var resp = await Client.PatchAsJsonAsync($"/api/v1/users/{aliceId}/nickname",
            new UpdateNicknameDto("StolenNick"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateNickname_DuplicateNickname_Returns400()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync(nickname: "AliceNick");
        await RegisterAndLoginAsync(nickname: "TakenNick");
        Authorize(aliceToken);

        var resp = await Client.PatchAsJsonAsync($"/api/v1/users/{aliceId}/nickname",
            new UpdateNicknameDto("TakenNick"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
