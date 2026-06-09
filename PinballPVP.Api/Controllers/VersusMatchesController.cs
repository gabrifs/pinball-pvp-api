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
public class VersusMatchesController(PinballPVPContext context) : ControllerBase
{
    private readonly PinballPVPContext _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<VersusMatchResponseDto>>> GetMatches(
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var result = await _context.VersusMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(VersusMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VersusMatchResponseDto>> GetMatch(int id)
    {
        var match = await _context.VersusMatches
            .AsNoTracking()
            .Where(match => match.Id == id)
            .Select(VersusMatchResponseDto.Projection)
            .FirstOrDefaultAsync();

        if (match == null)
            return NotFound();

        return Ok(match);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PagedResult<VersusMatchResponseDto>>> GetUserMatches(
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

        var result = await _context.VersusMatches
            .Where(m => m.WinnerId == userId || m.LoserId == userId)
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(VersusMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize);

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<VersusMatchResponseDto>> CreateMatch(CreateVersusMatchDto dto)
    {
        if (dto.WinnerId == dto.LoserId)
            return BadRequest("Winner and loser cannot be the same");

        var reporterId = User.GetUserId();
        if (reporterId != dto.WinnerId && reporterId != dto.LoserId)
            return Forbid();

        var users = await _context.Users
            .Include(user => user.PlayerRecord)
            .Where(user => user.Id == dto.WinnerId || user.Id == dto.LoserId)
            .ToListAsync();

        var winner = users.FirstOrDefault(user => user.Id == dto.WinnerId);
        var loser = users.FirstOrDefault(user => user.Id == dto.LoserId);

        if (winner == null || loser == null)
            return BadRequest("One or more users don't exist");

        var now = DateTime.UtcNow;
        var minPlayerId = Math.Min(dto.WinnerId, dto.LoserId);
        var maxPlayerId = Math.Max(dto.WinnerId, dto.LoserId);

        var pending = await _context.PendingVersusMatches
            .Where(p => p.MinPlayerId == minPlayerId && p.MaxPlayerId == maxPlayerId)
            .FirstOrDefaultAsync();

        // Treat an expired pending match as if it doesn't exist
        if (pending?.IsExpired == true)
        {
            _context.PendingVersusMatches.Remove(pending);
            await _context.SaveChangesAsync();
            pending = null;
        }

        if (pending is not null)
        {
            if (pending.ReporterId == reporterId)
                return BadRequest("You have already submitted this match result. Waiting for your opponent's confirmation.");

            // Second reporter: all six fields must match exactly
            var resultsMatch =
                pending.WinnerId        == dto.WinnerId        &&
                pending.LoserId         == dto.LoserId         &&
                pending.WinnerFinalScore == dto.WinnerFinalScore &&
                pending.WinnerRoundsWon  == dto.WinnerRoundsWon  &&
                pending.LoserFinalScore  == dto.LoserFinalScore  &&
                pending.LoserRoundsWon   == dto.LoserRoundsWon;

            _context.PendingVersusMatches.Remove(pending);

            if (!resultsMatch)
            {
                await _context.SaveChangesAsync();
                return Conflict("Results did not match. Both submissions have been discarded. You may start a new match.");
            }

            // Both reporters agree — commit the match
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

                PlayedAt = now
            };

            winner.PlayerRecord.VersusWins++;
            loser.PlayerRecord.VersusLosses++;

            winner.PlayerRecord.VersusHighscore = Math.Max(
                winner.PlayerRecord.VersusHighscore,
                dto.WinnerFinalScore);

            loser.PlayerRecord.VersusHighscore = Math.Max(
                loser.PlayerRecord.VersusHighscore,
                dto.LoserFinalScore);

            _context.VersusMatches.Add(match);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetMatch),
                new { id = match.Id },
                VersusMatchResponseDto.FromEntity(match));
        }

        // No active pending match — first reporter
        _context.PendingVersusMatches.Add(new PendingVersusMatch
        {
            ReporterId = reporterId,
            MinPlayerId = minPlayerId,
            MaxPlayerId = maxPlayerId,
            WinnerId = dto.WinnerId,
            LoserId = dto.LoserId,
            WinnerFinalScore = dto.WinnerFinalScore,
            WinnerRoundsWon = dto.WinnerRoundsWon,
            LoserFinalScore = dto.LoserFinalScore,
            LoserRoundsWon = dto.LoserRoundsWon,
            ExpiresAt = now.AddMinutes(PendingVersusMatch.ConfirmationWindowMinutes)
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Race: the other player submitted simultaneously — tell this player to retry as second reporter
            return Conflict("Another submission for this match is already pending. Please retry to confirm.");
        }

        return StatusCode(StatusCodes.Status202Accepted,
            "Match result submitted. Waiting for your opponent's confirmation.");
    }

}
