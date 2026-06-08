using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SoloMatchesController : ControllerBase
{
    private readonly PinballPVPContext _context;

    public SoloMatchesController(PinballPVPContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SoloMatchResponseDto>>> GetMatches(string? period)
    {
        IQueryable<SoloMatch> query = _context.SoloMatches;
        
        if(!IsValidPeriod(period))
            return BadRequest($"Invalid period: {period} (Valid periods are: week, month, year)");

        query = ApplyPeriodFilter(query, period);

        var matches = await query
            .AsNoTracking()
            .OrderByDescending(match => match.PlayedAt)
            .Select(SoloMatchResponseDto.Projection)
            .ToListAsync();

        return Ok(matches);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SoloMatchResponseDto>> GetMatch(int id)
    {
        var match = await _context.SoloMatches
            .AsNoTracking()
            .Where(match => match.Id == id)
            .Select(SoloMatchResponseDto.Projection)
            .FirstOrDefaultAsync();

        if(match == null)
            return NotFound();

        return Ok(match);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<SoloMatchResponseDto>>> GetUserMatches(int userId, string? period)
    {
        var userExists = await _context.Users
            .AnyAsync(user => user.Id == userId);

        if(!userExists)
            return NotFound();

        IQueryable<SoloMatch> query = _context.SoloMatches
            .Where(match =>
                match.UserId == userId);
        
        if(!IsValidPeriod(period))
            return BadRequest($"Invalid period: {period} (Valid periods are: week, month, year)");

        query = ApplyPeriodFilter(query, period);

        var matches = await query
            .AsNoTracking()
            .OrderByDescending(match => match.PlayedAt)
            .Select(SoloMatchResponseDto.Projection)
            .ToListAsync();

        return Ok(matches);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<SoloMatchResponseDto>> CreateMatch(CreateSoloMatchDto dto)
    {
        if(User.GetUserId() != dto.UserId)
            return Forbid();

        var user = await _context.Users
            .Include(user => user.PlayerRecord)
            .FirstOrDefaultAsync(user => user.Id == dto.UserId);

        if(user == null)
        {
            return BadRequest("User doesn't exist");
        }

        var match = new SoloMatch
        {
            UserId = dto.UserId,
            User = user,
            FinalScore = dto.FinalScore,
            RoundsWon = dto.RoundsWon,
            HasWon = dto.HasWon,

            PlayedAt = DateTime.UtcNow
        };

        // Update win/loss count
        if(match.HasWon)
            user.PlayerRecord.SoloWins++;
        else
            user.PlayerRecord.SoloLosses++;

        // Update Highscores
        user.PlayerRecord.SoloHighscore = Math.Max(
            user.PlayerRecord.SoloHighscore,
            dto.FinalScore
        );

        // Add match
        _context.SoloMatches.Add(match);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetMatch),
            new { id = match.Id },
            SoloMatchResponseDto.FromEntity(match)
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

    private static IQueryable<SoloMatch> ApplyPeriodFilter(IQueryable<SoloMatch> query, string? period)
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