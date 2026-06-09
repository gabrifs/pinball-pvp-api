using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Dtos.Leaderboards;
using PinballPVP.Api.Extensions;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class LeaderboardsController(PinballPVPContext context) : ControllerBase
{
    private readonly PinballPVPContext _context = context;

    [HttpGet("solo/highscore")]
    public async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloHighscoreLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return await GetSoloLeaderboardAsync(period, s => s.OrderByDescending(x => x.Highscore), page, pageSize);
    }

    [HttpGet("solo/wins")]
    public async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloWinsLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return await GetSoloLeaderboardAsync(period, s => s.OrderByDescending(x => x.Wins), page, pageSize);
    }

    [HttpGet("solo/winrate")]
    public async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloWinRateLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return await GetSoloLeaderboardAsync(
            period,
            s => s.OrderByDescending(x => (double)x.Wins / (x.Wins + x.Losses) * 100),
            page, pageSize);
    }

    [HttpGet("versus/highscore")]
    public async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusHighscoreLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return await GetVersusLeaderboardAsync(period, s => s.OrderByDescending(x => x.Highscore), page, pageSize);
    }

    [HttpGet("versus/wins")]
    public async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusWinsLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return await GetVersusLeaderboardAsync(period, s => s.OrderByDescending(x => x.Wins), page, pageSize);
    }

    [HttpGet("versus/winrate")]
    public async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusWinRateLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return await GetVersusLeaderboardAsync(
            period,
            s => s.OrderByDescending(x => (double)x.Wins / (x.Wins + x.Losses) * 100),
            page, pageSize);
    }

    [HttpGet("player/{userId}")]
    public async Task<ActionResult<PlayerRankDto>> GetPlayerRank(
        int userId,
        string? period = null)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var nickname = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Nickname)
            .FirstOrDefaultAsync();

        if (nickname == null)
            return NotFound();

        var soloStats   = await GetSoloStatsAsync(period);
        var versusStats = await GetVersusStatsAsync(period);

        var playerSolo = soloStats.FirstOrDefault(s => s.UserId == userId);
        SoloRankDto? soloRank = null;
        if (playerSolo != null)
        {
            var byHighscore = soloStats.OrderByDescending(x => x.Highscore).ToList();
            var byWins      = soloStats.OrderByDescending(x => x.Wins).ToList();
            var byWinRate   = soloStats.OrderByDescending(x => (double)x.Wins / (x.Wins + x.Losses)).ToList();
            soloRank = new SoloRankDto(
                playerSolo.Highscore,
                playerSolo.Wins,
                playerSolo.Losses,
                Math.Round((double)playerSolo.Wins / (playerSolo.Wins + playerSolo.Losses) * 100, 2),
                byHighscore.FindIndex(x => x.UserId == userId) + 1,
                byWins.FindIndex(x => x.UserId == userId) + 1,
                byWinRate.FindIndex(x => x.UserId == userId) + 1);
        }

        var playerVersus = versusStats.FirstOrDefault(s => s.UserId == userId);
        VersusRankDto? versusRank = null;
        if (playerVersus != null)
        {
            var byHighscore = versusStats.OrderByDescending(x => x.Highscore).ToList();
            var byWins      = versusStats.OrderByDescending(x => x.Wins).ToList();
            var byWinRate   = versusStats.OrderByDescending(x => (double)x.Wins / (x.Wins + x.Losses)).ToList();
            versusRank = new VersusRankDto(
                playerVersus.Highscore,
                playerVersus.Wins,
                playerVersus.Losses,
                Math.Round((double)playerVersus.Wins / (playerVersus.Wins + playerVersus.Losses) * 100, 2),
                byHighscore.FindIndex(x => x.UserId == userId) + 1,
                byWins.FindIndex(x => x.UserId == userId) + 1,
                byWinRate.FindIndex(x => x.UserId == userId) + 1);
        }

        return Ok(new PlayerRankDto(userId, nickname, soloRank, versusRank));
    }

    private async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloLeaderboardAsync(
        string? period,
        Func<IEnumerable<SoloStats>, IOrderedEnumerable<SoloStats>> orderBy,
        int page, int pageSize)
    {
        var allStats = await GetSoloStatsAsync(period);
        var sorted = orderBy(allStats).ToList();

        var ranked = sorted
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

        return Ok(new PagedResult<SoloLeaderboardEntryDto>(ranked, page, pageSize, sorted.Count));
    }

    private async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusLeaderboardAsync(
        string? period,
        Func<IEnumerable<VersusStats>, IOrderedEnumerable<VersusStats>> orderBy,
        int page, int pageSize)
    {
        var allStats = await GetVersusStatsAsync(period);
        var sorted = orderBy(allStats).ToList();

        var ranked = sorted
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

        return Ok(new PagedResult<VersusLeaderboardEntryDto>(ranked, page, pageSize, sorted.Count));
    }

    // Aggregates solo matches (with optional period filter) into per-player stats.
    private async Task<List<SoloStats>> GetSoloStatsAsync(string? period)
    {
        var raw = await _context.SoloMatches
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
            .ToListAsync();

        return [.. raw.Select(s => new SoloStats(s.UserId, s.Nickname, s.Highscore, s.Wins, s.Losses))];
    }

    // Aggregates versus matches by running two GroupBy queries (as winner, as loser) and merging in memory.
    private async Task<List<VersusStats>> GetVersusStatsAsync(string? period)
    {
        var filteredMatches = _context.VersusMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking();

        var asWinner = await filteredMatches
            .GroupBy(m => new { m.WinnerId, m.Winner.Nickname })
            .Select(g => new { UserId = g.Key.WinnerId, g.Key.Nickname, Wins = g.Count(), Highscore = g.Max(m => m.WinnerFinalScore) })
            .ToListAsync();

        var asLoser = await filteredMatches
            .GroupBy(m => new { m.LoserId, m.Loser.Nickname })
            .Select(g => new { UserId = g.Key.LoserId, g.Key.Nickname, Losses = g.Count(), Highscore = g.Max(m => m.LoserFinalScore) })
            .ToListAsync();

        var winnerById = asWinner.ToDictionary(x => x.UserId);
        var loserById  = asLoser.ToDictionary(x => x.UserId);

        return [.. winnerById.Keys.Union(loserById.Keys).Select(id =>
        {
            var w = winnerById.GetValueOrDefault(id);
            var l = loserById.GetValueOrDefault(id);
            return new VersusStats(
                id,
                w?.Nickname ?? l!.Nickname,
                Math.Max(w?.Highscore ?? 0, l?.Highscore ?? 0),
                w?.Wins ?? 0,
                l?.Losses ?? 0);
        })];
    }

    private sealed record SoloStats(int UserId, string Nickname, int Highscore, int Wins, int Losses);
    private sealed record VersusStats(int UserId, string Nickname, int Highscore, int Wins, int Losses);
}
