using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        // All-time: query PlayerRecord — incrementally maintained per match, so ORDER BY + LIMIT
        // stays in SQL at O(log n + LeaderboardCap) with an index rather than GROUP BY all matches.
        // Period-filtered: aggregate from SoloMatches (bounded by PlayedAt index, no full-table scan).
        var top = string.IsNullOrEmpty(period)
            ? await GetSoloTopFromRecordsAsync(sortBy, ct)
            : await GetSoloTopFromMatchesAsync(period, sortBy, ct);

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
        // All-time: query PlayerRecord (see GetSoloLeaderboardAsync comment for rationale).
        // Period-filtered: single FULL OUTER JOIN CTE — ORDER BY and LIMIT pushed into SQL.
        var top = string.IsNullOrEmpty(period)
            ? await GetVersusTopFromRecordsAsync(sortBy, ct)
            : await GetVersusTopFromMatchesAsync(period, sortBy, ct);

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

        // All-time: query PlayerRecord (more efficient than aggregating all match rows).
        // Period-filtered: aggregate from match tables (bounded by PlayedAt index).
        var soloStats   = string.IsNullOrEmpty(period) ? await GetAllTimeSoloStatsAsync(ct)   : await GetSoloStatsAsync(period, ct);
        var versusStats = string.IsNullOrEmpty(period) ? await GetAllTimeVersusStatsAsync(ct) : await GetVersusStatsAsync(period, ct);

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

    // All-time solo top-N: queries PlayerRecord, sort + LIMIT pushed into SQL.
    private async Task<List<LeaderboardStats>> GetSoloTopFromRecordsAsync(LeaderboardSortBy sortBy, CancellationToken ct)
    {
        var baseQuery = context.PlayerRecords
            .AsNoTracking()
            .Where(r => r.SoloWins + r.SoloLosses > 0);

        var raw = await (sortBy switch
        {
            LeaderboardSortBy.Highscore => baseQuery
                .OrderByDescending(r => r.SoloHighscore),
            LeaderboardSortBy.Wins => baseQuery
                .OrderByDescending(r => r.SoloWins),
            LeaderboardSortBy.WinRate => baseQuery
                .Where(r => r.SoloWins + r.SoloLosses >= _winRateMinMatches)
                .OrderByDescending(r => (double)r.SoloWins / (r.SoloWins + r.SoloLosses)),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
        }).Take(LeaderboardCap)
          .Select(r => new { r.UserId, r.User.Nickname, r.SoloHighscore, r.SoloWins, r.SoloLosses })
          .ToListAsync(ct);

        return [.. raw.Select(r => new LeaderboardStats(r.UserId, r.Nickname, r.SoloHighscore, r.SoloWins, r.SoloLosses))];
    }

    // Period-filtered solo top-N: aggregates from SoloMatches, sort + LIMIT pushed into SQL.
    private async Task<List<LeaderboardStats>> GetSoloTopFromMatchesAsync(string period, LeaderboardSortBy sortBy, CancellationToken ct)
    {
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

        var raw = await (sortBy switch
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

        return [.. raw.Select(r => new LeaderboardStats(r.UserId, r.Nickname, r.Highscore, r.Wins, r.Losses))];
    }

    // All-time versus top-N: queries PlayerRecord, sort + LIMIT pushed into SQL.
    private async Task<List<LeaderboardStats>> GetVersusTopFromRecordsAsync(LeaderboardSortBy sortBy, CancellationToken ct)
    {
        var baseQuery = context.PlayerRecords
            .AsNoTracking()
            .Where(r => r.VersusWins + r.VersusLosses > 0);

        var raw = await (sortBy switch
        {
            LeaderboardSortBy.Highscore => baseQuery
                .OrderByDescending(r => r.VersusHighscore),
            LeaderboardSortBy.Wins => baseQuery
                .OrderByDescending(r => r.VersusWins),
            LeaderboardSortBy.WinRate => baseQuery
                .Where(r => r.VersusWins + r.VersusLosses >= _winRateMinMatches)
                .OrderByDescending(r => (double)r.VersusWins / (r.VersusWins + r.VersusLosses)),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
        }).Take(LeaderboardCap)
          .Select(r => new { r.UserId, r.User.Nickname, r.VersusHighscore, r.VersusWins, r.VersusLosses })
          .ToListAsync(ct);

        return [.. raw.Select(r => new LeaderboardStats(r.UserId, r.Nickname, r.VersusHighscore, r.VersusWins, r.VersusLosses))];
    }

    // All-time solo stats for all players, from PlayerRecord (for rank computation).
    private async Task<List<LeaderboardStats>> GetAllTimeSoloStatsAsync(CancellationToken ct)
    {
        var raw = await context.PlayerRecords
            .AsNoTracking()
            .Where(r => r.SoloWins + r.SoloLosses > 0)
            .Select(r => new { r.UserId, r.User.Nickname, r.SoloHighscore, r.SoloWins, r.SoloLosses })
            .ToListAsync(ct);

        return [.. raw.Select(r => new LeaderboardStats(r.UserId, r.Nickname, r.SoloHighscore, r.SoloWins, r.SoloLosses))];
    }

    // All-time versus stats for all players, from PlayerRecord (for rank computation).
    private async Task<List<LeaderboardStats>> GetAllTimeVersusStatsAsync(CancellationToken ct)
    {
        var raw = await context.PlayerRecords
            .AsNoTracking()
            .Where(r => r.VersusWins + r.VersusLosses > 0)
            .Select(r => new { r.UserId, r.User.Nickname, r.VersusHighscore, r.VersusWins, r.VersusLosses })
            .ToListAsync(ct);

        return [.. raw.Select(r => new LeaderboardStats(r.UserId, r.Nickname, r.VersusHighscore, r.VersusWins, r.VersusLosses))];
    }

    // Period-filtered solo stats for all players, from SoloMatches (for rank computation).
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

    // Period-filtered versus top-N: single FULL OUTER JOIN CTE — ORDER BY and LIMIT pushed into SQL.
    // GetVersusStatsAsync (below) still handles the rank-computation path, where a cap cannot apply.
    private async Task<List<LeaderboardStats>> GetVersusTopFromMatchesAsync(
        string period, LeaderboardSortBy sortBy, CancellationToken ct)
    {
        var (start, end) = PeriodFilterExtensions.GetPeriodRange(period);

        var (winRateWhere, orderBy) = sortBy switch
        {
            LeaderboardSortBy.Highscore => (
                string.Empty,
                "GREATEST(COALESCE(w.winner_highscore, 0), COALESCE(l.loser_highscore, 0)) DESC"),
            LeaderboardSortBy.Wins => (
                string.Empty,
                "COALESCE(w.wins, 0) DESC"),
            LeaderboardSortBy.WinRate => (
                "WHERE COALESCE(w.wins, 0) + COALESCE(l.losses, 0) >= @minMatches",
                "COALESCE(w.wins, 0)::double precision / (COALESCE(w.wins, 0) + COALESCE(l.losses, 0)) DESC"),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
        };

        var endFilter = end.HasValue ? "AND vm.played_at < @end" : string.Empty;

        var sql = $"""
            WITH winners AS (
                SELECT vm.winner_id,
                       u.nickname,
                       COUNT(*)::int             AS wins,
                       MAX(vm.winner_final_score) AS winner_highscore
                FROM   versus_matches vm
                JOIN   users u ON u.id = vm.winner_id
                WHERE  vm.played_at >= @start {endFilter}
                GROUP  BY vm.winner_id, u.nickname
            ),
            losers AS (
                SELECT vm.loser_id,
                       u.nickname,
                       COUNT(*)::int             AS losses,
                       MAX(vm.loser_final_score)  AS loser_highscore
                FROM   versus_matches vm
                JOIN   users u ON u.id = vm.loser_id
                WHERE  vm.played_at >= @start {endFilter}
                GROUP  BY vm.loser_id, u.nickname
            )
            -- Aliases must be snake_case: UseSnakeCaseNamingConvention() applies to
            -- SqlQueryRaw<T> column mapping, so EF Core expects user_id not "UserId".
            SELECT COALESCE(w.winner_id,        l.loser_id)    AS user_id,
                   COALESCE(w.nickname,         l.nickname)    AS nickname,
                   COALESCE(w.wins,   0)                       AS wins,
                   COALESCE(l.losses, 0)                       AS losses,
                   GREATEST(COALESCE(w.winner_highscore, 0),
                            COALESCE(l.loser_highscore,  0))   AS highscore
            FROM   winners w
            FULL   OUTER JOIN losers l ON l.loser_id = w.winner_id
            {winRateWhere}
            ORDER  BY {orderBy}
            LIMIT  {LeaderboardCap}
            """;

        var parameters = new List<NpgsqlParameter> { new("start", start) };
        if (end.HasValue)
            parameters.Add(new("end", end.Value));
        if (sortBy == LeaderboardSortBy.WinRate)
            parameters.Add(new("minMatches", _winRateMinMatches));

        return [.. await context.Database
            .SqlQueryRaw<LeaderboardStats>(sql, [.. parameters])
            .ToListAsync(ct)];
    }

    // Period-filtered versus stats for all players, via two GroupBy queries merged in memory (for rank computation).
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

}

// Used by LeaderboardService for both LINQ projections and SqlQueryRaw materialisation.
// Must be internal (not private nested) so EF Core's expression-tree compiler can access it.
internal sealed record LeaderboardStats(int UserId, string Nickname, int Highscore, int Wins, int Losses);
