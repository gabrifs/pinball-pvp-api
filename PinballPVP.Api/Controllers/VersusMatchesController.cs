using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Services.VersusMatches;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class VersusMatchesController(IVersusMatchService versusMatchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<VersusMatchResponseDto>>> GetMatches(
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        return Ok(await versusMatchService.GetMatchesAsync(period, page, pageSize));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VersusMatchResponseDto>> GetMatch(int id)
    {
        var match = await versusMatchService.GetMatchAsync(id);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PagedResult<VersusMatchResponseDto>>> GetUserMatches(
        int userId,
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var result = await versusMatchService.GetUserMatchesAsync(userId, period, page, pageSize);
        return result is null ? NotFound() : Ok(result);
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

        var result = await versusMatchService.CreateMatchAsync(reporterId, dto);

        return result.Error switch
        {
            CreateVersusMatchError.None => CreatedAtAction(nameof(GetMatch), new { id = result.Match!.Id }, result.Match),
            CreateVersusMatchError.Pending => StatusCode(StatusCodes.Status202Accepted, "Match result submitted. Waiting for your opponent's confirmation."),
            CreateVersusMatchError.UsersNotFound => BadRequest("One or more users don't exist"),
            CreateVersusMatchError.AlreadyPending => BadRequest("You have already submitted this match result. Waiting for your opponent's confirmation."),
            CreateVersusMatchError.ResultsMismatch => Conflict("Results did not match. Both submissions have been discarded. You may start a new match."),
            CreateVersusMatchError.PendingConflict => Conflict("Another submission for this match is already pending. Please retry to confirm."),
            _ => BadRequest()
        };
    }
}
