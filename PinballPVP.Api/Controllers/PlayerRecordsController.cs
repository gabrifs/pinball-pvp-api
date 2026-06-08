using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/users/[controller]")]
public class PlayerRecordsController : ControllerBase
{
    private readonly PinballPVPContext _context;

    public PlayerRecordsController(PinballPVPContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlayerRecordResponseDto>> GetPlayerRecord(int id)
    {
        var playerRecord = await _context.PlayerRecords
            .AsNoTracking()
            .Where(playerRecord => playerRecord.UserId == id)
            .Select(PlayerRecordResponseDto.Projection)
            .FirstOrDefaultAsync();

        if(playerRecord == null)
            return NotFound();

        return Ok(playerRecord);
    }
}