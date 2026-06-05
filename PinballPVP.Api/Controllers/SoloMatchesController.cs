using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
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
    public async Task<ActionResult<SoloMatch>> GetMatches(string? period)
    {
        IQueryable<SoloMatch> query = _context.SoloMatches;
        
        if(!string.IsNullOrEmpty(period))
        {
            var now = DateTime.UtcNow.Date;

            switch (period)
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

        var matches = await query
            .OrderBy(match => match.PlayedAt)
            .ToListAsync();

        return Ok(matches);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SoloMatch>> GetMatch(int id)
    {
        var matches = _context.SoloMatches
            .FirstOrDefaultAsync(match => match.Id == id);

        return Ok(matches);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<SoloMatch>> GetUserMatches(int userId, string? period)
    {
        IQueryable<SoloMatch> query = _context.SoloMatches
            .Where(match => match.UserId == userId);
        
        if(!string.IsNullOrEmpty(period))
        {
            var now = DateTime.UtcNow.Date;

            switch (period)
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

        var matches = await query
            .OrderBy(match => match.PlayedAt)
            .ToListAsync();

        return Ok(matches);
    }

    [HttpPost]
    public async Task<ActionResult> CreateMatch(CreateSoloMatchDto dto)
    {
        var user = await _context.Users
            .Include(user => user.PlayerRecord)
            .FirstOrDefaultAsync(user => user.Id == dto.UserId);

        if(user == null)
        {
            return BadRequest("User don't exist");
        }

        var match = new SoloMatch
        {
            UserId = dto.UserId,
            FinalScore = dto.FinalScore,
            RoundsWon = dto.RoundsWon,
            HasWon = dto.HasWon,

            PlayedAt = DateTime.UtcNow
        };

        // Update win/loss count
        if (match.HasWon)
        {
            user.PlayerRecord.SoloWins++;
        }
        else
        {
            user.PlayerRecord.SoloLosses++;
        }

        // Update Highscores
        user.PlayerRecord.SoloHighscore = Math.Max(
            user.PlayerRecord.SoloHighscore,
            dto.FinalScore
        );

        // Add match
        _context.SoloMatches.Add(match);

        await _context.SaveChangesAsync();

        return Ok(match);
    }
}