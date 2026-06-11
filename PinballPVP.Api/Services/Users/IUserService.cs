using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Services.Users;

public interface IUserService
{
    Task<PagedResult<UserResponseDto>> GetUsersAsync(int page, int pageSize, CancellationToken ct = default);

    Task<UserResponseDto?> GetUserAsync(int id, CancellationToken ct = default);

    Task<CreateUserResult> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default);

    Task<UpdateNicknameResult> UpdateNicknameAsync(int userId, UpdateNicknameDto dto, CancellationToken ct = default);
}

public enum CreateUserError
{
    None,
    UsernameInUse,
    NicknameInUse,
    EmailInUse,
    DuplicateDetails
}

public record CreateUserResult(CreateUserError Error, UserResponseDto? User)
{
    public bool Succeeded => Error == CreateUserError.None;

    public static CreateUserResult Success(UserResponseDto user) => new(CreateUserError.None, user);
    public static CreateUserResult Failure(CreateUserError error) => new(error, null);
}

public enum UpdateNicknameError
{
    None,
    UserNotFound,
    NicknameInUse
}

public record UpdateNicknameResult(UpdateNicknameError Error, UserResponseDto? User)
{
    public bool Succeeded => Error == UpdateNicknameError.None;

    public static UpdateNicknameResult Success(UserResponseDto user) => new(UpdateNicknameError.None, user);
    public static UpdateNicknameResult Failure(UpdateNicknameError error) => new(error, null);
}
