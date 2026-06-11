using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Services.SoloMatches;

public interface ISoloMatchService
{
    Task<PagedResult<SoloMatchResponseDto>> GetMatchesAsync(
        string? period, int page, int pageSize, CancellationToken ct = default);

    Task<SoloMatchResponseDto?> GetMatchAsync(int id, CancellationToken ct = default);

    Task<PagedResult<SoloMatchResponseDto>?> GetUserMatchesAsync(
        int userId, string? period, int page, int pageSize, CancellationToken ct = default);

    Task<CreateSoloMatchResult> CreateMatchAsync(CreateSoloMatchDto dto, CancellationToken ct = default);
}

public enum CreateSoloMatchError
{
    None,
    UserNotFound
}

public record CreateSoloMatchResult(CreateSoloMatchError Error, SoloMatchResponseDto? Match)
{
    public bool Succeeded => Error == CreateSoloMatchError.None;

    public static CreateSoloMatchResult Success(SoloMatchResponseDto match) => new(CreateSoloMatchError.None, match);
    public static CreateSoloMatchResult Failure(CreateSoloMatchError error) => new(error, null);
}
