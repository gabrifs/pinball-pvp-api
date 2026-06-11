using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Dtos.Leaderboards;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Services.Leaderboards;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class LeaderboardsController(ILeaderboardService leaderboardService) : ControllerBase
{
    [HttpGet("solo/highscore")]
    public async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloHighscoreLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return Ok(await leaderboardService.GetSoloLeaderboardAsync(period, LeaderboardSortBy.Highscore, page, pageSize));
    }

    [HttpGet("solo/wins")]
    public async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloWinsLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return Ok(await leaderboardService.GetSoloLeaderboardAsync(period, LeaderboardSortBy.Wins, page, pageSize));
    }

    [HttpGet("solo/winrate")]
    public async Task<ActionResult<PagedResult<SoloLeaderboardEntryDto>>> GetSoloWinRateLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return Ok(await leaderboardService.GetSoloLeaderboardAsync(period, LeaderboardSortBy.WinRate, page, pageSize));
    }

    [HttpGet("versus/highscore")]
    public async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusHighscoreLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return Ok(await leaderboardService.GetVersusLeaderboardAsync(period, LeaderboardSortBy.Highscore, page, pageSize));
    }

    [HttpGet("versus/wins")]
    public async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusWinsLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return Ok(await leaderboardService.GetVersusLeaderboardAsync(period, LeaderboardSortBy.Wins, page, pageSize));
    }

    [HttpGet("versus/winrate")]
    public async Task<ActionResult<PagedResult<VersusLeaderboardEntryDto>>> GetVersusWinRateLeaderboard(
        string? period = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");
        return Ok(await leaderboardService.GetVersusLeaderboardAsync(period, LeaderboardSortBy.WinRate, page, pageSize));
    }

    [HttpGet("yearly/{year}")]
    public async Task<ActionResult<YearlyLeaderboardResponseDto>> GetYearlyLeaderboard(int year)
    {
        var result = await leaderboardService.GetYearlyLeaderboardAsync(year);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("player/{userId}")]
    public async Task<ActionResult<PlayerRankDto>> GetPlayerRank(
        int userId,
        string? period = null)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var result = await leaderboardService.GetPlayerRankAsync(userId, period);
        return result is null ? NotFound() : Ok(result);
    }
}
