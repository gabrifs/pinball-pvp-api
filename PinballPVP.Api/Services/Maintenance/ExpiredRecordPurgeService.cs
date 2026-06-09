using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;

namespace PinballPVP.Api.Services.Maintenance;

public class ExpiredRecordPurgeService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ExpiredRecordPurgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = configuration.GetValue<int>("Maintenance:PurgeIntervalHours", 24);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during expired record purge");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PinballPVPContext>();
        var now = DateTime.UtcNow;

        var deletedTokens = await context.RefreshTokens
            .Where(t => t.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var deletedPending = await context.PendingVersusMatches
            .Where(p => p.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        if (deletedTokens > 0 || deletedPending > 0)
            logger.LogInformation(
                "Purged {Tokens} expired refresh token(s) and {Pending} expired pending match(es)",
                deletedTokens, deletedPending);
    }
}
