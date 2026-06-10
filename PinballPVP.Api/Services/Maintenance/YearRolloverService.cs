using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Enums;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Services.Maintenance;

public class YearRolloverService(
    PinballPVPContext context,
    IConfiguration configuration,
    ILogger<YearRolloverService> logger) : IYearRolloverService
{
    public async Task ProcessAsync(CancellationToken ct = default)
    {
        var currentYearStart = PeriodFilterExtensions.GetYearRange(DateTime.UtcNow.Year).Start;

        var soloYears = context.SoloMatches
            .Where(m => m.PlayedAt < currentYearStart)
            .Select(m => m.PlayedAt.Year);

        var versusYears = context.VersusMatches
            .Where(m => m.PlayedAt < currentYearStart)
            .Select(m => m.PlayedAt.Year);

        var priorYears = await soloYears
            .Union(versusYears)
            .Distinct()
            .OrderBy(year => year)
            .ToListAsync(ct);

        foreach (var year in priorYears)
            await ProcessYearAsync(year, ct);
    }

    // ExecuteAsync is required by NpgsqlRetryingExecutionStrategy when using explicit transactions.
    private async Task ProcessYearAsync(int year, CancellationToken ct)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(innerCt => RolloverYearAsync(year, innerCt), ct);
    }

    private async Task RolloverYearAsync(int year, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var snapshots = await context.PlayerRecords
            .AsNoTracking()
            .Select(pr => new PlayerRecordSnapshot(
                pr.UserId,
                pr.User.Nickname,
                pr.SoloHighscore,
                pr.SoloWins,
                pr.SoloLosses,
                pr.VersusHighscore,
                pr.VersusWins,
                pr.VersusLosses))
            .ToListAsync(ct);

        var winRateMinMatches = configuration.GetValue("Leaderboard:WinRateMinMatches", 10);

        var entries = new List<YearlyLeaderboardEntry>();
        entries.AddRange(TopThree(snapshots, year, YearlyLeaderboardCategory.SoloHighscore,
            r => r.SoloWins + r.SoloLosses > 0, r => r.SoloHighscore));
        entries.AddRange(TopThree(snapshots, year, YearlyLeaderboardCategory.SoloWins,
            r => r.SoloWins + r.SoloLosses > 0, r => r.SoloWins));
        entries.AddRange(TopThree(snapshots, year, YearlyLeaderboardCategory.SoloWinRate,
            r => r.SoloWins + r.SoloLosses >= winRateMinMatches,
            r => Math.Round((double)r.SoloWins / (r.SoloWins + r.SoloLosses) * 100, 2)));
        entries.AddRange(TopThree(snapshots, year, YearlyLeaderboardCategory.VersusHighscore,
            r => r.VersusWins + r.VersusLosses > 0, r => r.VersusHighscore));
        entries.AddRange(TopThree(snapshots, year, YearlyLeaderboardCategory.VersusWins,
            r => r.VersusWins + r.VersusLosses > 0, r => r.VersusWins));
        entries.AddRange(TopThree(snapshots, year, YearlyLeaderboardCategory.VersusWinRate,
            r => r.VersusWins + r.VersusLosses >= winRateMinMatches,
            r => Math.Round((double)r.VersusWins / (r.VersusWins + r.VersusLosses) * 100, 2)));

        context.YearlyLeaderboardEntries.AddRange(entries);
        await context.SaveChangesAsync(ct);

        await context.PlayerRecords.ExecuteUpdateAsync(s => s
            .SetProperty(pr => pr.SoloWins, 0)
            .SetProperty(pr => pr.SoloLosses, 0)
            .SetProperty(pr => pr.SoloHighscore, 0)
            .SetProperty(pr => pr.VersusWins, 0)
            .SetProperty(pr => pr.VersusLosses, 0)
            .SetProperty(pr => pr.VersusHighscore, 0), ct);

        var (start, end) = PeriodFilterExtensions.GetYearRange(year);

        var deletedSolo = await context.SoloMatches
            .Where(m => m.PlayedAt >= start && m.PlayedAt < end)
            .ExecuteDeleteAsync(ct);

        var deletedVersus = await context.VersusMatches
            .Where(m => m.PlayedAt >= start && m.PlayedAt < end)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Yearly rollover for {Year}: captured {Entries} leaderboard entries, reset PlayerRecords, " +
            "and pruned {SoloMatches} solo and {VersusMatches} versus match(es)",
            year, entries.Count, deletedSolo, deletedVersus);
    }

    // Highscore/Wins categories qualify with Wins + Losses > 0 (mirrors the live leaderboards' guarantee
    // that players with no matches don't appear). WinRate categories qualify at Leaderboard:WinRateMinMatches.
    private static List<YearlyLeaderboardEntry> TopThree(
        List<PlayerRecordSnapshot> records,
        int year,
        YearlyLeaderboardCategory category,
        Func<PlayerRecordSnapshot, bool> qualifies,
        Func<PlayerRecordSnapshot, double> valueSelector)
    {
        return records
            .Where(qualifies)
            .OrderByDescending(valueSelector)
            .Take(3)
            .Select((record, index) => new YearlyLeaderboardEntry
            {
                Year = year,
                Category = category,
                Rank = index + 1,
                UserId = record.UserId,
                NicknameSnapshot = record.Nickname,
                Value = valueSelector(record)
            })
            .ToList();
    }

    private record PlayerRecordSnapshot(
        int UserId,
        string Nickname,
        int SoloHighscore,
        int SoloWins,
        int SoloLosses,
        int VersusHighscore,
        int VersusWins,
        int VersusLosses);
}
