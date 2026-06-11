using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Services.Auth;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginDto dto, CancellationToken ct = default);

    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task<LogoutError> LogoutAsync(int userId, string refreshToken, CancellationToken ct = default);

    Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);

    Task<ResetPasswordError> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
}

public enum LoginError
{
    None,
    InvalidCredentials
}

public record LoginResult(LoginError Error, LoginResponseDto? Tokens)
{
    public bool Succeeded => Error == LoginError.None;

    public static LoginResult Success(LoginResponseDto tokens) => new(LoginError.None, tokens);
    public static LoginResult Failure(LoginError error) => new(error, null);
}

public enum RefreshError
{
    None,
    InvalidToken,
    UserNotFound
}

public record RefreshResult(RefreshError Error, LoginResponseDto? Tokens)
{
    public bool Succeeded => Error == RefreshError.None;

    public static RefreshResult Success(LoginResponseDto tokens) => new(RefreshError.None, tokens);
    public static RefreshResult Failure(RefreshError error) => new(error, null);
}

public enum LogoutError
{
    None,
    Forbidden
}

public enum ResetPasswordError
{
    None,
    InvalidOrExpiredCode
}
