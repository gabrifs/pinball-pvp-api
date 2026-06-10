using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Dtos.Leaderboards;
using PinballPVP.Api.Models;
using PinballPVP.Api.Services.Maintenance;
using PinballPVP.Tests.Infrastructure;

namespace PinballPVP.Tests.Integration;

public class YearRolloverTests(PinballApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ProcessAsync_RollsOverPriorYear_SnapshotsTopThreeResetsRecordsAndPrunesMatches()
    {
        var lastYear = DateTime.UtcNow.AddYears(-1).Year;
        var lastYearPlayedAt = DateTime.UtcNow.AddYears(-1);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var aliceNickname   = $"Alice_{suffix}";
        var bobNickname     = $"Bob_{suffix}";
        var charlieNickname = $"Charlie_{suffix}";
        var daveNickname    = $"Dave_{suffix}";

        var (aliceId, _, _)   = await RegisterAndLoginAsync(nickname: aliceNickname);
        var (bobId, _, _)     = await RegisterAndLoginAsync(nickname: bobNickname);
        var (charlieId, _, _) = await RegisterAndLoginAsync(nickname: charlieNickname);
        var (daveId, daveToken, _) = await RegisterAndLoginAsync(nickname: daveNickname);

        // Seed PlayerRecord aggregates directly, plus one match dated last year so the rollover
        // service detects `lastYear` as a prior year to process.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PinballPVPContext>();

            var alice = await db.PlayerRecords.FirstAsync(pr => pr.UserId == aliceId);
            alice.SoloWins = 5; alice.SoloLosses = 0; alice.SoloHighscore = 5000;
            alice.VersusWins = 4; alice.VersusLosses = 1; alice.VersusHighscore = 8000;

            var bob = await db.PlayerRecords.FirstAsync(pr => pr.UserId == bobId);
            bob.SoloWins = 3; bob.SoloLosses = 2; bob.SoloHighscore = 4000;
            bob.VersusWins = 2; bob.VersusLosses = 3; bob.VersusHighscore = 7000;

            // Charlie has only 1 match in each mode -> below the WinRateMinMatches threshold (3),
            // so excluded from the WinRate categories despite a 100% win rate.
            var charlie = await db.PlayerRecords.FirstAsync(pr => pr.UserId == charlieId);
            charlie.SoloWins = 1; charlie.SoloLosses = 0; charlie.SoloHighscore = 3000;
            charlie.VersusWins = 1; charlie.VersusLosses = 0; charlie.VersusHighscore = 6000;

            // Dave has no matches in either mode -> excluded from every category.

            db.SoloMatches.Add(new SoloMatch
            {
                UserId = aliceId,
                FinalScore = 5000,
                RoundsWon = 3,
                HasWon = true,
                PlayedAt = lastYearPlayedAt
            });

            await db.SaveChangesAsync();
        }

        // Current-year match, to verify it survives the rollover untouched.
        Authorize(daveToken);
        await Client.PostAsJsonAsync("/api/v1/solomatches", new CreateSoloMatchDto(daveId, 100, 1, false));
        Deauthorize();

        using (var scope = Factory.Services.CreateScope())
        {
            var rolloverService = scope.ServiceProvider.GetRequiredService<IYearRolloverService>();
            await rolloverService.ProcessAsync(CancellationToken.None);
        }

        var leaderboard = await Client.GetFromJsonAsync<YearlyLeaderboardResponseDto>(
            $"/api/v1/leaderboards/yearly/{lastYear}");

        Assert.NotNull(leaderboard);
        Assert.Equal(lastYear, leaderboard!.Year);

        Assert.Equal(3, leaderboard.SoloHighscore.Count);
        Assert.Equal((1, aliceNickname, 5000.0), (leaderboard.SoloHighscore[0].Rank, leaderboard.SoloHighscore[0].Nickname, leaderboard.SoloHighscore[0].Value));
        Assert.Equal((2, bobNickname, 4000.0), (leaderboard.SoloHighscore[1].Rank, leaderboard.SoloHighscore[1].Nickname, leaderboard.SoloHighscore[1].Value));
        Assert.Equal((3, charlieNickname, 3000.0), (leaderboard.SoloHighscore[2].Rank, leaderboard.SoloHighscore[2].Nickname, leaderboard.SoloHighscore[2].Value));

        Assert.Equal(3, leaderboard.SoloWins.Count);
        Assert.Equal((1, aliceNickname, 5.0), (leaderboard.SoloWins[0].Rank, leaderboard.SoloWins[0].Nickname, leaderboard.SoloWins[0].Value));
        Assert.Equal((2, bobNickname, 3.0), (leaderboard.SoloWins[1].Rank, leaderboard.SoloWins[1].Nickname, leaderboard.SoloWins[1].Value));
        Assert.Equal((3, charlieNickname, 1.0), (leaderboard.SoloWins[2].Rank, leaderboard.SoloWins[2].Nickname, leaderboard.SoloWins[2].Value));

        // Charlie excluded: only 1 match, below WinRateMinMatches (3).
        Assert.Equal(2, leaderboard.SoloWinRate.Count);
        Assert.Equal((1, aliceNickname, 100.0), (leaderboard.SoloWinRate[0].Rank, leaderboard.SoloWinRate[0].Nickname, leaderboard.SoloWinRate[0].Value));
        Assert.Equal((2, bobNickname, 60.0), (leaderboard.SoloWinRate[1].Rank, leaderboard.SoloWinRate[1].Nickname, leaderboard.SoloWinRate[1].Value));

        Assert.Equal(3, leaderboard.VersusHighscore.Count);
        Assert.Equal((1, aliceNickname, 8000.0), (leaderboard.VersusHighscore[0].Rank, leaderboard.VersusHighscore[0].Nickname, leaderboard.VersusHighscore[0].Value));
        Assert.Equal((2, bobNickname, 7000.0), (leaderboard.VersusHighscore[1].Rank, leaderboard.VersusHighscore[1].Nickname, leaderboard.VersusHighscore[1].Value));
        Assert.Equal((3, charlieNickname, 6000.0), (leaderboard.VersusHighscore[2].Rank, leaderboard.VersusHighscore[2].Nickname, leaderboard.VersusHighscore[2].Value));

        Assert.Equal(3, leaderboard.VersusWins.Count);
        Assert.Equal((1, aliceNickname, 4.0), (leaderboard.VersusWins[0].Rank, leaderboard.VersusWins[0].Nickname, leaderboard.VersusWins[0].Value));
        Assert.Equal((2, bobNickname, 2.0), (leaderboard.VersusWins[1].Rank, leaderboard.VersusWins[1].Nickname, leaderboard.VersusWins[1].Value));
        Assert.Equal((3, charlieNickname, 1.0), (leaderboard.VersusWins[2].Rank, leaderboard.VersusWins[2].Nickname, leaderboard.VersusWins[2].Value));

        // Charlie excluded: only 1 match, below WinRateMinMatches (3).
        Assert.Equal(2, leaderboard.VersusWinRate.Count);
        Assert.Equal((1, aliceNickname, 80.0), (leaderboard.VersusWinRate[0].Rank, leaderboard.VersusWinRate[0].Nickname, leaderboard.VersusWinRate[0].Value));
        Assert.Equal((2, bobNickname, 40.0), (leaderboard.VersusWinRate[1].Rank, leaderboard.VersusWinRate[1].Nickname, leaderboard.VersusWinRate[1].Value));

        // PlayerRecords reset to zero for everyone after the rollover.
        foreach (var userId in new[] { aliceId, bobId, charlieId, daveId })
        {
            var record = await Client.GetFromJsonAsync<PlayerRecordResponseDto>(
                $"/api/v1/users/playerrecords/{userId}");

            Assert.Equal(0, record!.SoloWins);
            Assert.Equal(0, record.SoloLosses);
            Assert.Equal(0, record.SoloHighscore);
            Assert.Equal(0, record.VersusWins);
            Assert.Equal(0, record.VersusLosses);
            Assert.Equal(0, record.VersusHighscore);
        }

        // Last year's matches pruned, current-year match untouched.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PinballPVPContext>();

            var lastYearMatches = await db.SoloMatches.CountAsync(m => m.PlayedAt.Year == lastYear);
            Assert.Equal(0, lastYearMatches);

            var currentYearMatches = await db.SoloMatches.CountAsync(m => m.PlayedAt.Year == DateTime.UtcNow.Year);
            Assert.Equal(1, currentYearMatches);
        }
    }
}
