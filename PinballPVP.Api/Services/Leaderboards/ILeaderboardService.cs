using PinballPVP.Api.Dtos;
using PinballPVP.Api.Dtos.Leaderboards;

namespace PinballPVP.Api.Services.Leaderboards;

public interface ILeaderboardService
{
    Task<PagedResult<SoloLeaderboardEntryDto>> GetSoloLeaderboardAsync(
        string? period, LeaderboardSortBy sortBy, int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<VersusLeaderboardEntryDto>> GetVersusLeaderboardAsync(
        string? period, LeaderboardSortBy sortBy, int page, int pageSize, CancellationToken ct = default);

    Task<YearlyLeaderboardResponseDto?> GetYearlyLeaderboardAsync(int year, CancellationToken ct = default);

    Task<PlayerRankDto?> GetPlayerRankAsync(int userId, string? period, CancellationToken ct = default);
}

public enum LeaderboardSortBy
{
    Highscore,
    Wins,
    WinRate
}
