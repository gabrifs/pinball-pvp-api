using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Services.PlayerRecords;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/users/playerrecords")]
public class PlayerRecordsController(IPlayerRecordService playerRecordService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<PlayerRecordResponseDto>> GetPlayerRecord(int id)
    {
        var playerRecord = await playerRecordService.GetPlayerRecordAsync(id);

        if (playerRecord == null)
            return NotFound();

        return Ok(playerRecord);
    }
}
