using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Services.PlayerRecords;

public class PlayerRecordService(PinballPVPContext context) : IPlayerRecordService
{
    public async Task<PlayerRecordResponseDto?> GetPlayerRecordAsync(int userId, CancellationToken ct = default)
    {
        return await context.PlayerRecords
            .AsNoTracking()
            .Where(pr => pr.UserId == userId)
            .Select(PlayerRecordResponseDto.Projection)
            .FirstOrDefaultAsync(ct);
    }
}
