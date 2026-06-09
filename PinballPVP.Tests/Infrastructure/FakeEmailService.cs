using PinballPVP.Api.Services.Email;

namespace PinballPVP.Tests.Infrastructure;

public class FakeEmailService : IEmailService
{
    public string? LastRecoveryCode { get; private set; }

    public Task SendPasswordRecoveryAsync(string toEmail, string toNickname, string recoveryCode, CancellationToken ct = default)
    {
        LastRecoveryCode = recoveryCode;
        return Task.CompletedTask;
    }

    public void Reset() => LastRecoveryCode = null;
}
