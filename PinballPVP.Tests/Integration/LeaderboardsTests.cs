using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Dtos.Leaderboards;
using PinballPVP.Api.Enums;
using PinballPVP.Api.Models;
using PinballPVP.Tests.Infrastructure;

namespace PinballPVP.Tests.Integration;

public class LeaderboardsTests(PinballApiFactory factory) : IntegrationTestBase(factory)
{
    // Test config sets Leaderboard:WinRateMinMatches to 3 (see PinballApiFactory).
    private const int WinRateMinMatches = 3;

    private async Task CreateSoloMatchAsync(int userId, string token, int finalScore, bool hasWon)
    {
        Authorize(token);
        var resp = await Client.PostAsJsonAsync("/api/v1/solomatches",
            new CreateSoloMatchDto(userId, finalScore, hasWon ? 3 : 1, hasWon));
        resp.EnsureSuccessStatusCode();
    }

    private async Task PlayVersusMatchAsync(
        int winnerId, string winnerToken, int winnerScore,
        int loserId, string loserToken, int loserScore)
    {
        var dto = new CreateVersusMatchDto(winnerId, winnerScore, 3, loserId, loserScore, 1);

        Authorize(winnerToken);
        await Client.PostAsJsonAsync("/api/v1/versusmatches", dto);

        Authorize(loserToken);
        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", dto);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetSoloWinRateLeaderboard_ExcludesPlayersBelowMinMatchesThreshold()
    {
        // Alice: 1 match, 100% win rate, but below the threshold -> excluded
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        await CreateSoloMatchAsync(aliceId, aliceToken, 1000, true);

        // Bob: meets the threshold with a lower win rate -> included
        var (bobId, bobToken, _) = await RegisterAndLoginAsync();
        await CreateSoloMatchAsync(bobId, bobToken, 1000, true);
        await CreateSoloMatchAsync(bobId, bobToken, 900, true);
        await CreateSoloMatchAsync(bobId, bobToken, 800, false);

        var result = await Client.GetFromJsonAsync<PagedResult<SoloLeaderboardEntryDto>>(
            "/api/v1/leaderboards/solo/winrate");

        Assert.Contains(result!.Items, e => e.UserId == bobId);
        Assert.DoesNotContain(result.Items, e => e.UserId == aliceId);
    }

    [Fact]
    public async Task GetSoloHighscoreLeaderboard_IncludesPlayersBelowWinRateThreshold()
    {
        // The match-count threshold only applies to the WinRate leaderboard.
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        await CreateSoloMatchAsync(aliceId, aliceToken, 1000, true);

        var result = await Client.GetFromJsonAsync<PagedResult<SoloLeaderboardEntryDto>>(
            "/api/v1/leaderboards/solo/highscore");

        Assert.Contains(result!.Items, e => e.UserId == aliceId);
    }

    [Fact]
    public async Task GetPlayerRank_BelowWinRateThreshold_HasZeroWinRateRankButRankedElsewhere()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        await CreateSoloMatchAsync(aliceId, aliceToken, 1000, true);

        var rank = await Client.GetFromJsonAsync<PlayerRankDto>($"/api/v1/leaderboards/player/{aliceId}");

        Assert.NotNull(rank!.Solo);
        Assert.Equal(0, rank.Solo!.WinRateRank);
        Assert.Equal(1, rank.Solo.HighscoreRank);
        Assert.Equal(1, rank.Solo.WinsRank);
    }

    [Fact]
    public async Task GetPlayerRank_AtWinRateThreshold_HasNonZeroWinRateRank()
    {
        var (bobId, bobToken, _) = await RegisterAndLoginAsync();
        for (var i = 0; i < WinRateMinMatches; i++)
            await CreateSoloMatchAsync(bobId, bobToken, 1000 * (i + 1), true);

        var rank = await Client.GetFromJsonAsync<PlayerRankDto>($"/api/v1/leaderboards/player/{bobId}");

        Assert.NotNull(rank!.Solo);
        Assert.Equal(1, rank.Solo!.WinRateRank);
    }

    [Fact]
    public async Task GetVersusWinRateLeaderboard_ExcludesPlayersBelowMinMatchesThreshold()
    {
        var (aliceId, aliceToken, _)     = await RegisterAndLoginAsync();
        var (bobId, bobToken, _)         = await RegisterAndLoginAsync();
        var (charlieId, charlieToken, _) = await RegisterAndLoginAsync();

        // Alice beats Bob once -> Alice has 1 match (100%), below the threshold -> excluded
        await PlayVersusMatchAsync(aliceId, aliceToken, 8000, bobId, bobToken, 5000);

        // Charlie and Bob play enough matches to each reach the threshold
        await PlayVersusMatchAsync(charlieId, charlieToken, 8000, bobId, bobToken, 5000);
        await PlayVersusMatchAsync(charlieId, charlieToken, 7000, bobId, bobToken, 4000);
        await PlayVersusMatchAsync(bobId, bobToken, 6000, charlieId, charlieToken, 3000);

        var result = await Client.GetFromJsonAsync<PagedResult<VersusLeaderboardEntryDto>>(
            "/api/v1/leaderboards/versus/winrate");

        Assert.Contains(result!.Items, e => e.UserId == charlieId);
        Assert.Contains(result.Items, e => e.UserId == bobId);
        Assert.DoesNotContain(result.Items, e => e.UserId == aliceId);
    }

    [Fact]
    public async Task GetYearlyLeaderboard_NoEntriesForYear_Returns404()
    {
        var resp = await Client.GetAsync("/api/v1/leaderboards/yearly/1999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetYearlyLeaderboard_WithEntries_Returns200GroupedByCategory()
    {
        const int year = 2020;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PinballPVPContext>();
            db.YearlyLeaderboardEntries.AddRange(
                new YearlyLeaderboardEntry
                {
                    Year = year, Category = YearlyLeaderboardCategory.SoloHighscore,
                    Rank = 1, UserId = null, NicknameSnapshot = "Alice", Value = 5000
                },
                new YearlyLeaderboardEntry
                {
                    Year = year, Category = YearlyLeaderboardCategory.SoloWinRate,
                    Rank = 1, UserId = null, NicknameSnapshot = "Bob", Value = 75.5
                });
            await db.SaveChangesAsync();
        }

        var result = await Client.GetFromJsonAsync<YearlyLeaderboardResponseDto>(
            $"/api/v1/leaderboards/yearly/{year}");

        Assert.NotNull(result);
        Assert.Equal(year, result!.Year);

        Assert.Single(result.SoloHighscore);
        Assert.Equal("Alice", result.SoloHighscore[0].Nickname);
        Assert.Equal(5000.0, result.SoloHighscore[0].Value);

        Assert.Single(result.SoloWinRate);
        Assert.Equal("Bob", result.SoloWinRate[0].Nickname);
        Assert.Equal(75.5, result.SoloWinRate[0].Value);

        Assert.Empty(result.SoloWins);
        Assert.Empty(result.VersusHighscore);
        Assert.Empty(result.VersusWins);
        Assert.Empty(result.VersusWinRate);
    }
}
