namespace PinballPVP.Api.Services.Email;

public interface IEmailService
{
    Task SendPasswordRecoveryAsync(string toEmail, string toNickname, string recoveryCode, int expirationMinutes, CancellationToken ct = default);
}
