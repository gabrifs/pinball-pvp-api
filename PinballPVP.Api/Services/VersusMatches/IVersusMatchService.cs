using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Services.VersusMatches;

public interface IVersusMatchService
{
    Task<PagedResult<VersusMatchResponseDto>> GetMatchesAsync(
        string? period, int page, int pageSize, CancellationToken ct = default);

    Task<VersusMatchResponseDto?> GetMatchAsync(int id, CancellationToken ct = default);

    Task<PagedResult<VersusMatchResponseDto>?> GetUserMatchesAsync(
        int userId, string? period, int page, int pageSize, CancellationToken ct = default);

    Task<CreateVersusMatchResult> CreateMatchAsync(int reporterId, CreateVersusMatchDto dto, CancellationToken ct = default);
}

public enum CreateVersusMatchError
{
    None,
    Pending,
    UsersNotFound,
    AlreadyPending,
    ResultsMismatch,
    PendingConflict
}

public record CreateVersusMatchResult(CreateVersusMatchError Error, VersusMatchResponseDto? Match)
{
    public bool Succeeded => Error == CreateVersusMatchError.None;

    public static CreateVersusMatchResult Created(VersusMatchResponseDto match) => new(CreateVersusMatchError.None, match);
    public static CreateVersusMatchResult AcceptedPending() => new(CreateVersusMatchError.Pending, null);
    public static CreateVersusMatchResult Failure(CreateVersusMatchError error) => new(error, null);
}
