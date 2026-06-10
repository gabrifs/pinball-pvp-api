namespace PinballPVP.Api.Services.Maintenance;

public interface IYearRolloverService
{
    // Detects and processes any prior-year rollovers: snapshots top-3 leaderboards,
    // resets PlayerRecord aggregates, and prunes that year's matches.
    Task ProcessAsync(CancellationToken ct = default);
}
