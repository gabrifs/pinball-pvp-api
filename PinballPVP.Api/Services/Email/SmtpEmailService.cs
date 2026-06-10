using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PinballPVP.Api.Services.Email;

public class SmtpEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendPasswordRecoveryAsync(string toEmail, string toNickname, string recoveryCode, int expirationMinutes, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            configuration["Email:FromName"] ?? "PinballPVP",
            configuration["Email:FromAddress"]!));
        message.To.Add(new MailboxAddress(toNickname, toEmail));
        message.Subject = "PinballPVP — Password Recovery";
        message.Body = new TextPart("plain")
        {
            Text = $"""
                Hi {toNickname},

                Your password recovery code is:

                {recoveryCode}

                This code expires in {expirationMinutes} minutes. If you didn't request a password reset, you can safely ignore this email.

                — PinballPVP
                """
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            configuration["Email:Host"]!,
            configuration.GetValue<int>("Email:Port", 587),
            SecureSocketOptions.StartTls,
            ct);
        await client.AuthenticateAsync(
            configuration["Email:Username"]!,
            configuration["Email:Password"]!,
            ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
