using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Models;
using PinballPVP.Tests.Infrastructure;

namespace PinballPVP.Tests.Integration;

public class SoloMatchesTests(PinballApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateMatch_Unauthenticated_Returns401()
    {
        var (userId, _, _) = await RegisterAndLoginAsync();

        var resp = await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 1000, 2, true));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_AsDifferentUser_Returns403()
    {
        var (aliceId, _, _) = await RegisterAndLoginAsync();
        var (_, bobToken, _) = await RegisterAndLoginAsync();
        Authorize(bobToken);

        var resp = await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(aliceId, 1000, 2, true));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_Win_Returns201AndIncrementsWinsAndHighscore()
    {
        var (userId, token, _) = await RegisterAndLoginAsync();
        Authorize(token);

        var resp = await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 5000, 3, true));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var record = await Client.GetFromJsonAsync<PlayerRecordResponseDto>(
            $"/api/v1/users/playerrecords/{userId}");

        Assert.Equal(1, record!.SoloWins);
        Assert.Equal(0, record.SoloLosses);
        Assert.Equal(5000, record.SoloHighscore);

        var currentYear = DateTime.UtcNow.Year;
        Assert.Equal(5000, record.AllTimeBest.SoloHighscore);
        Assert.Equal(currentYear, record.AllTimeBest.SoloHighscoreYear);
        Assert.Equal(1, record.AllTimeBest.SoloWins);
        Assert.Equal(currentYear, record.AllTimeBest.SoloWinsYear);
        Assert.Equal(1, record.AllTimeBest.SoloMatchesPlayed);
        Assert.Equal(currentYear, record.AllTimeBest.SoloMatchesPlayedYear);
    }

    [Fact]
    public async Task CreateMatch_Loss_Returns201AndIncrementsLossesOnly()
    {
        var (userId, token, _) = await RegisterAndLoginAsync();
        Authorize(token);

        var resp = await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 1200, 1, false));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var record = await Client.GetFromJsonAsync<PlayerRecordResponseDto>(
            $"/api/v1/users/playerrecords/{userId}");

        Assert.Equal(0, record!.SoloWins);
        Assert.Equal(1, record.SoloLosses);
        Assert.Equal(1200, record.SoloHighscore); // Highscore updates on both win/loss
    }

    [Fact]
    public async Task CreateMatch_HighscoreOnlyMovesUp()
    {
        var (userId, token, _) = await RegisterAndLoginAsync();
        Authorize(token);

        await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 5000, 3, true));
        await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 2000, 2, true)); // Lower score

        var record = await Client.GetFromJsonAsync<PlayerRecordResponseDto>(
            $"/api/v1/users/playerrecords/{userId}");

        Assert.Equal(5000, record!.SoloHighscore); // Stays at the peak
        Assert.Equal(5000, record.AllTimeBest.SoloHighscore); // Stays at the peak
    }

    [Fact]
    public async Task GetMatches_Returns200WithPagedResult()
    {
        var (userId, token, _) = await RegisterAndLoginAsync();
        Authorize(token);
        await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 1000, 1, true));

        var resp = await Client.GetAsync("/api/v1/solomatches");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<PagedResult<SoloMatchResponseDto>>();
        Assert.Equal(1, body!.TotalCount);
    }

    [Fact]
    public async Task GetMatches_InvalidPeriod_Returns400()
    {
        var resp = await Client.GetAsync("/api/v1/solomatches?period=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetUserMatches_PeriodFilter_ExcludesOldMatches()
    {
        var (userId, token, _) = await RegisterAndLoginAsync();
        Authorize(token);

        // Insert a match from two years ago directly via the DB (controller always stamps UtcNow)
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PinballPVPContext>();
            db.SoloMatches.Add(new SoloMatch
            {
                UserId = userId,
                FinalScore = 500,
                RoundsWon = 1,
                HasWon = true,
                PlayedAt = DateTime.UtcNow.AddYears(-2)
            });
            await db.SaveChangesAsync();
        }

        // Current-year match via the controller
        await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, 1000, 2, true));

        var yearResp = await Client.GetFromJsonAsync<PagedResult<SoloMatchResponseDto>>(
            $"/api/v1/solomatches/user/{userId}?period=year");
        Assert.Equal(1, yearResp!.TotalCount);

        var allResp = await Client.GetFromJsonAsync<PagedResult<SoloMatchResponseDto>>(
            $"/api/v1/solomatches/user/{userId}");
        Assert.Equal(2, allResp!.TotalCount);
    }
}
