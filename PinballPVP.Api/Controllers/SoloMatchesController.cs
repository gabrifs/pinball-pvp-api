using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class SoloMatchesController(PinballPVPContext context) : ControllerBase
{
    private readonly PinballPVPContext _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<SoloMatchResponseDto>>> GetMatches(
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var result = await _context.SoloMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(SoloMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SoloMatchResponseDto>> GetMatch(int id)
    {
        var match = await _context.SoloMatches
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(SoloMatchResponseDto.Projection)
            .FirstOrDefaultAsync();

        if (match == null)
            return NotFound();

        return Ok(match);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PagedResult<SoloMatchResponseDto>>> GetUserMatches(
        int userId,
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return NotFound();

        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var result = await _context.SoloMatches
            .Where(m => m.UserId == userId)
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(SoloMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize);

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<SoloMatchResponseDto>> CreateMatch(CreateSoloMatchDto dto)
    {
        if (User.GetUserId() != dto.UserId)
            return Forbid();

        var user = await _context.Users
            .Include(u => u.PlayerRecord)
            .FirstOrDefaultAsync(u => u.Id == dto.UserId);

        if (user == null)
            return BadRequest("User doesn't exist");

        var match = new SoloMatch
        {
            UserId = dto.UserId,
            User = user,
            FinalScore = dto.FinalScore,
            RoundsWon = dto.RoundsWon,
            HasWon = dto.HasWon,
            PlayedAt = DateTime.UtcNow
        };

        if (match.HasWon)
            user.PlayerRecord.SoloWins++;
        else
            user.PlayerRecord.SoloLosses++;

        user.PlayerRecord.SoloHighscore = Math.Max(user.PlayerRecord.SoloHighscore, dto.FinalScore);

        _context.SoloMatches.Add(match);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMatch), new { id = match.Id }, SoloMatchResponseDto.FromEntity(match));
    }
}
