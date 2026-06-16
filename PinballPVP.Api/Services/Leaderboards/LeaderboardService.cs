using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Dtos.Leaderboards;
using PinballPVP.Api.Enums;
using PinballPVP.Api.Extensions;

namespace PinballPVP.Api.Services.Leaderboards;

public class LeaderboardService(PinballPVPContext context, IConfiguration configuration) : ILeaderboardService
{
    // Players need at least this many matches in the period to appear on a WinRate leaderboard —
    // otherwise a single win (100% with 1 match) would top the board over established players.
    private readonly int _winRateMinMatches = configuration.GetValue("Leaderboard:WinRateMinMatches", 10);

    // Leaderboards are capped at this many entries; pagination operates within this fixed window.
    private const int LeaderboardCap = 100;

    public async Task<PagedResult<SoloLeaderboardEntryDto>> GetSoloLeaderboardAsync(
        string? period, LeaderboardSortBy sortBy, int page, int pageSize, CancellationToken ct = default)
    {
        // Push sort + cap into SQL so we never load more than LeaderboardCap rows.
        var baseQuery = context.SoloMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .GroupBy(m => new { m.UserId, m.User.Nickname })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Nickname,
                Highscore = g.Max(m => m.FinalScore),
                Wins      = g.Count(m => m.HasWon),
                Losses    = g.Count(m => !m.HasWon)
            });

        var top = await (sortBy switch
        {
            LeaderboardSortBy.Highscore => baseQuery
                .OrderByDescending(x => x.Highscore),
            LeaderboardSortBy.Wins => baseQuery
                .OrderByDescending(x => x.Wins),
            LeaderboardSortBy.WinRate => baseQuery
                .Where(x => x.Wins + x.Losses >= _winRateMinMatches)
                .OrderByDescending(x => (double)x.Wins / (x.Wins + x.Losses)),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
        }).Take(LeaderboardCap).ToListAsync(ct);

        var ranked = top
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select((item, i) => new SoloLeaderboardEntryDto(
                (page - 1) * pageSize + i + 1,
                item.UserId,
                item.Nickname,
                item.Highscore,
                item.Wins,
                item.Losses,
                Math.Round((double)item.Wins / (item.Wins + item.Losses) * 100, 2)))
            .ToList();

        return new PagedResult<SoloLeaderboardEntryDto>(ranked, page, pageSize, top.Count);
    }

    public async Task<PagedResult<VersusLeaderboardEntryDto>> GetVersusLeaderboardAsync(
        string? period, LeaderboardSortBy sortBy, int page, int pageSize, CancellationToken ct = default)
    {
        // The two-query winner/loser merge prevents pushing the cap to SQL without a raw FULL OUTER
        // JOIN; GetVersusStatsAsync loads all players but the sorted result is capped here.
        var allStats = await GetVersusStatsAsync(period, ct);
        var top = ApplySort(allStats, sortBy).Take(LeaderboardCap).ToList();

        var ranked = top
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select((item, i) => new VersusLeaderboardEntryDto(
                (page - 1) * pageSize + i + 1,
                item.UserId,
                item.Nickname,
                item.Highscore,
                item.Wins,
                item.Losses,
                Math.Round((double)item.Wins / (item.Wins + item.Losses) * 100, 2)))
            .ToList();

        return new PagedResult<VersusLeaderboardEntryDto>(ranked, page, pageSize, top.Count);
    }

    public async Task<YearlyLeaderboardResponseDto?> GetYearlyLeaderboardAsync(int year, CancellationToken ct = default)
    {
        if (!await context.YearlyLeaderboardEntries.AnyAsync(e => e.Year == year, ct))
            return null;

        return new YearlyLeaderboardResponseDto(
            year,
            await GetYearlyCategoryAsync(year, YearlyLeaderboardCategory.SoloHighscore, ct),
            await GetYearlyCategoryAsync(year, YearlyLeaderboardCategory.SoloWins, ct),
            await GetYearlyCategoryAsync(year, YearlyLeaderboardCategory.SoloWinRate, ct),
            await GetYearlyCategoryAsync(year, YearlyLeaderboardCategory.VersusHighscore, ct),
            await GetYearlyCategoryAsync(year, YearlyLeaderboardCategory.VersusWins, ct),
            await GetYearlyCategoryAsync(year, YearlyLeaderboardCategory.VersusWinRate, ct));
    }

    public async Task<PlayerRankDto?> GetPlayerRankAsync(int userId, string? period, CancellationToken ct = default)
    {
        var nickname = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Nickname)
            .FirstOrDefaultAsync(ct);

        if (nickname == null)
            return null;

        var soloStats   = await GetSoloStatsAsync(period, ct);
        var versusStats = await GetVersusStatsAsync(period, ct);

        var playerSolo = soloStats.FirstOrDefault(s => s.UserId == userId);
        SoloRankDto? soloRank = null;
        if (playerSolo != null)
        {
            var (highscoreRank, winsRank, winRateRank) = ComputeRanks(soloStats, userId);
            soloRank = new SoloRankDto(
                playerSolo.Highscore,
                playerSolo.Wins,
                playerSolo.Losses,
                Math.Round((double)playerSolo.Wins / (playerSolo.Wins + playerSolo.Losses) * 100, 2),
                highscoreRank,
                winsRank,
                winRateRank);
        }

        var playerVersus = versusStats.FirstOrDefault(s => s.UserId == userId);
        VersusRankDto? versusRank = null;
        if (playerVersus != null)
        {
            var (highscoreRank, winsRank, winRateRank) = ComputeRanks(versusStats, userId);
            versusRank = new VersusRankDto(
                playerVersus.Highscore,
                playerVersus.Wins,
                playerVersus.Losses,
                Math.Round((double)playerVersus.Wins / (playerVersus.Wins + playerVersus.Losses) * 100, 2),
                highscoreRank,
                winsRank,
                winRateRank);
        }

        return new PlayerRankDto(userId, nickname, soloRank, versusRank);
    }

    private (int HighscoreRank, int WinsRank, int WinRateRank) ComputeRanks(List<LeaderboardStats> stats, int userId)
    {
        var byHighscore = ApplySort(stats, LeaderboardSortBy.Highscore).ToList();
        var byWins      = ApplySort(stats, LeaderboardSortBy.Wins).ToList();
        var byWinRate   = ApplySort(stats, LeaderboardSortBy.WinRate).ToList();

        return (
            byHighscore.FindIndex(x => x.UserId == userId) + 1,
            byWins.FindIndex(x => x.UserId == userId) + 1,
            byWinRate.FindIndex(x => x.UserId == userId) + 1);
    }

    private IOrderedEnumerable<LeaderboardStats> ApplySort(IEnumerable<LeaderboardStats> stats, LeaderboardSortBy sortBy) =>
        sortBy switch
        {
            LeaderboardSortBy.Highscore => stats.OrderByDescending(x => x.Highscore),
            LeaderboardSortBy.Wins      => stats.OrderByDescending(x => x.Wins),
            LeaderboardSortBy.WinRate   => stats
                .Where(x => x.Wins + x.Losses >= _winRateMinMatches)
                .OrderByDescending(x => (double)x.Wins / (x.Wins + x.Losses) * 100),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
        };

    private async Task<List<YearlyLeaderboardEntryDto>> GetYearlyCategoryAsync(
        int year, YearlyLeaderboardCategory category, CancellationToken ct)
    {
        return await context.YearlyLeaderboardEntries
            .AsNoTracking()
            .Where(e => e.Year == year && e.Category == category)
            .OrderBy(e => e.Rank)
            .Select(YearlyLeaderboardEntryDto.Projection)
            .ToListAsync(ct);
    }

    // Aggregates solo matches (with optional period filter) into per-player stats.
    private async Task<List<LeaderboardStats>> GetSoloStatsAsync(string? period, CancellationToken ct)
    {
        var raw = await context.SoloMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .GroupBy(m => new { m.UserId, m.User.Nickname })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Nickname,
                Highscore = g.Max(m => m.FinalScore),
                Wins      = g.Count(m => m.HasWon),
                Losses    = g.Count(m => !m.HasWon)
            })
            .ToListAsync(ct);

        return [.. raw.Select(s => new LeaderboardStats(s.UserId, s.Nickname, s.Highscore, s.Wins, s.Losses))];
    }

    // Aggregates versus matches by running two GroupBy queries (as winner, as loser) and merging in memory.
    private async Task<List<LeaderboardStats>> GetVersusStatsAsync(string? period, CancellationToken ct)
    {
        var filteredMatches = context.VersusMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking();

        var asWinner = await filteredMatches
            .GroupBy(m => new { m.WinnerId, m.Winner.Nickname })
            .Select(g => new { UserId = g.Key.WinnerId, g.Key.Nickname, Wins = g.Count(), Highscore = g.Max(m => m.WinnerFinalScore) })
            .ToListAsync(ct);

        var asLoser = await filteredMatches
            .GroupBy(m => new { m.LoserId, m.Loser.Nickname })
            .Select(g => new { UserId = g.Key.LoserId, g.Key.Nickname, Losses = g.Count(), Highscore = g.Max(m => m.LoserFinalScore) })
            .ToListAsync(ct);

        var winnerById = asWinner.ToDictionary(x => x.UserId);
        var loserById  = asLoser.ToDictionary(x => x.UserId);

        return [.. winnerById.Keys.Union(loserById.Keys).Select(id =>
        {
            var w = winnerById.GetValueOrDefault(id);
            var l = loserById.GetValueOrDefault(id);
            return new LeaderboardStats(
                id,
                w?.Nickname ?? l!.Nickname,
                Math.Max(w?.Highscore ?? 0, l?.Highscore ?? 0),
                w?.Wins ?? 0,
                l?.Losses ?? 0);
        })];
    }

    private sealed record LeaderboardStats(int UserId, string Nickname, int Highscore, int Wins, int Losses);
}
