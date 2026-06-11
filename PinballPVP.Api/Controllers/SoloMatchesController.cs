using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Services.SoloMatches;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class SoloMatchesController(ISoloMatchService soloMatchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SoloMatchResponseDto>>> GetMatches(
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        return Ok(await soloMatchService.GetMatchesAsync(period, page, pageSize));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SoloMatchResponseDto>> GetMatch(int id)
    {
        var match = await soloMatchService.GetMatchAsync(id);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PagedResult<SoloMatchResponseDto>>> GetUserMatches(
        int userId,
        string? period,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        if (!period.IsValidPeriod())
            return BadRequest($"Invalid period: {period} (valid values: week, month, year)");

        var result = await soloMatchService.GetUserMatchesAsync(userId, period, page, pageSize);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<SoloMatchResponseDto>> CreateMatch(CreateSoloMatchDto dto)
    {
        if (User.GetUserId() != dto.UserId)
            return Forbid();

        var result = await soloMatchService.CreateMatchAsync(dto);

        return result.Error switch
        {
            CreateSoloMatchError.None => CreatedAtAction(nameof(GetMatch), new { id = result.Match!.Id }, result.Match),
            CreateSoloMatchError.UserNotFound => BadRequest("User doesn't exist"),
            _ => BadRequest()
        };
    }
}
