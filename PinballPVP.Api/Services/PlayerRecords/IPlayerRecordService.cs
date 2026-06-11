using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Services.PlayerRecords;

public interface IPlayerRecordService
{
    Task<PlayerRecordResponseDto?> GetPlayerRecordAsync(int userId, CancellationToken ct = default);
}
