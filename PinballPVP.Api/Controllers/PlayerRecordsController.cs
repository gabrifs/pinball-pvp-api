using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/users/playerrecords")]
public class PlayerRecordsController(PinballPVPContext context) : ControllerBase
{
    private readonly PinballPVPContext _context = context;

    [HttpGet("{id}")]
    public async Task<ActionResult<PlayerRecordResponseDto>> GetPlayerRecord(int id)
    {
        var playerRecord = await _context.PlayerRecords
            .AsNoTracking()
            .Where(pr => pr.UserId == id)
            .Select(PlayerRecordResponseDto.Projection)
            .FirstOrDefaultAsync();

        if (playerRecord == null)
            return NotFound();

        return Ok(playerRecord);
    }
}
