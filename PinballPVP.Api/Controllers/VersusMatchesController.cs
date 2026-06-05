using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersusMatchesController : ControllerBase
{
    private readonly PinballPVPContext _context;

    public VersusMatchesController(PinballPVPContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<VersusMatchResponseDto>>> GetMatches(string? period)
    {
        IQueryable<VersusMatch> query = _context.VersusMatches;
        
        if(!IsValidPeriod(period))
            return BadRequest($"Invalid period: {period} (Valid periods are: week, month, year)");

        query = ApplyPeriodFilter(query, period);

        var matches = await query
            .AsNoTracking()
            .OrderByDescending(match => match.PlayedAt)
            .Select(VersusMatchResponseDto.Projection)
            .ToListAsync();

        return Ok(matches);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VersusMatchResponseDto>> GetMatch(int id)
    {
        var match = await _context.VersusMatches
            .AsNoTracking()
            .Where(match => match.Id == id)
            .Select(VersusMatchResponseDto.Projection)
            .FirstOrDefaultAsync();

        if(match == null)
            return NotFound();

        return Ok(match);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<VersusMatchResponseDto>>> GetUserMatches(int userId, string? period)
    {
        var userExists = await _context.Users
            .AnyAsync(user => user.Id == userId);

        if(!userExists)
            return NotFound();

        IQueryable<VersusMatch> query = _context.VersusMatches
            .Where(match =>
                match.WinnerId == userId ||
                match.LoserId == userId);
        
        if(!IsValidPeriod(period))
            return BadRequest($"Invalid period: {period} (Valid periods are: week, month, year)");

        query = ApplyPeriodFilter(query, period);

        var matches = await query
            .AsNoTracking()
            .OrderByDescending(match => match.PlayedAt)
            .Select(VersusMatchResponseDto.Projection)
            .ToListAsync();

        return Ok(matches);
    }

    [HttpPost]
    public async Task<ActionResult<VersusMatchResponseDto>> CreateMatch(CreateVersusMatchDto dto)
    {
        if(dto.WinnerId == dto.LoserId)
        {
            return BadRequest("Winner and loser cannot be the same");
        }

        var users = await _context.Users
            .Include(user => user.PlayerRecord)
            .Where(user => 
                user.Id == dto.WinnerId || 
                user.Id == dto.LoserId)
            .ToListAsync();

        var winner = users.FirstOrDefault(user => user.Id == dto.WinnerId);
        var loser = users.FirstOrDefault(user => user.Id == dto.LoserId);

        if(winner == null || loser == null)
        {
            return BadRequest("One or more users don't exist");
        }

        var match = new VersusMatch
        {
            WinnerId = dto.WinnerId,
            Winner = winner,
            WinnerFinalScore = dto.WinnerFinalScore,
            WinnerRoundsWon = dto.WinnerRoundsWon,

            LoserId = dto.LoserId,
            Loser = loser,
            LoserFinalScore = dto.LoserFinalScore,
            LoserRoundsWon = dto.LoserRoundsWon,

            PlayedAt = DateTime.UtcNow
        };

        // Update win/loss count
        winner.PlayerRecord.VersusWins++;
        loser.PlayerRecord.VersusLosses++;

        // Update Highscores
        winner.PlayerRecord.VersusHighscore = Math.Max(
            winner.PlayerRecord.VersusHighscore,
            dto.WinnerFinalScore
        );

        loser.PlayerRecord.VersusHighscore = Math.Max(
            loser.PlayerRecord.VersusHighscore,
            dto.LoserFinalScore
        );

        // Add match
        _context.VersusMatches.Add(match);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetMatch),
            new { id = match.Id },
            VersusMatchResponseDto.FromEntity(match)
        );
    }

    private static bool IsValidPeriod(string? period)
    {
        if(string.IsNullOrEmpty(period))
            return true;

        switch (period.ToLower())
            {
                case "week":
                case "month":
                case "year":
                    {
                        return true;
                    }

                default:
                    {
                        return false;
                    }
            }
    }

    private static IQueryable<VersusMatch> ApplyPeriodFilter(IQueryable<VersusMatch> query, string? period)
    {
        if(!string.IsNullOrEmpty(period))
        {
            var now = DateTime.UtcNow.Date;

            switch (period.ToLower())
            {
                case "week":
                    {
                        var startOfWeek = now
                            .AddDays(-(int)now.DayOfWeek);

                        query = query
                            .Where(match => 
                                match.PlayedAt >= startOfWeek);
                        break;
                    }

                case "month":
                    {
                        var startOfMonth = new DateTime(
                            now.Year,
                            now.Month,
                            1);

                        var startOfNextMonth = startOfMonth.AddMonths(1);

                        query = query
                            .Where(match => 
                                match.PlayedAt >= startOfMonth &&
                                match.PlayedAt < startOfNextMonth);
                        break;
                    }

                case "year":
                    {
                        var startOfYear = new DateTime(
                            now.Year, 1, 1);

                        var startOfNextYear = startOfYear.AddYears(1);

                        query = query
                            .Where(match => 
                                match.PlayedAt >= startOfYear &&
                                match.PlayedAt < startOfNextYear);
                        break;
                    }
            }
        }

        return query;
    }
}