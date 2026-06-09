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

    // Aggregates from SoloMatches (with optional period filter) so the result reflects only
    // matches played in that window, not all-time PlayerRecord totals.
    private async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloLeaderboardAsync(
        string? period,
        Func<IEnumerable<SoloStats>, IOrderedEnumerable<SoloStats>> orderBy,
        int page, int pageSize)
    {
        var rawStats = await _context.SoloMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .GroupBy(m => new { m.UserId, m.User.Nickname })
            .Select(g => new
            {
                UserId = g.Key.UserId,
                Nickname = g.Key.Nickname,
                Highscore = g.Max(m => m.FinalScore),
                Wins = g.Count(m => m.HasWon),
                Losses = g.Count(m => !m.HasWon)
            })
            .ToListAsync();

        var sorted = orderBy(rawStats.Select(s => new SoloStats(s.UserId, s.Nickname, s.Highscore, s.Wins, s.Losses))).ToList();

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

    // Versus: each match contributes to both participants. Two GroupBy queries (as winner, as loser)
    // are run against the same period-filtered base and merged in memory.
    private async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusLeaderboardAsync(
        string? period,
        Func<IEnumerable<VersusStats>, IOrderedEnumerable<VersusStats>> orderBy,
        int page, int pageSize)
    {
        var filteredMatches = _context.VersusMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking();

        var asWinner = await filteredMatches
            .GroupBy(m => new { m.WinnerId, m.Winner.Nickname })
            .Select(g => new { UserId = g.Key.WinnerId, Nickname = g.Key.Nickname, Wins = g.Count(), Highscore = g.Max(m => m.WinnerFinalScore) })
            .ToListAsync();

        var asLoser = await filteredMatches
            .GroupBy(m => new { m.LoserId, m.Loser.Nickname })
            .Select(g => new { UserId = g.Key.LoserId, Nickname = g.Key.Nickname, Losses = g.Count(), Highscore = g.Max(m => m.LoserFinalScore) })
            .ToListAsync();

        var winnerById = asWinner.ToDictionary(x => x.UserId);
        var loserById  = asLoser.ToDictionary(x => x.UserId);

        var stats = winnerById.Keys.Union(loserById.Keys).Select(id =>
        {
            var w = winnerById.GetValueOrDefault(id);
            var l = loserById.GetValueOrDefault(id);
            return new VersusStats(
                id,
                w?.Nickname ?? l!.Nickname,
                Math.Max(w?.Highscore ?? 0, l?.Highscore ?? 0),
                w?.Wins ?? 0,
                l?.Losses ?? 0);
        });

        var sorted = orderBy(stats).ToList();

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

    private sealed record SoloStats(int UserId, string Nickname, int Highscore, int Wins, int Losses);
    private sealed record VersusStats(int UserId, string Nickname, int Highscore, int Wins, int Losses);
}
