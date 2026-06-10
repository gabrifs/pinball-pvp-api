using Microsoft.Extensions.Diagnostics.HealthChecks;
using PinballPVP.Api.Data;

namespace PinballPVP.Api.Services.Health;

public class DatabaseHealthCheck(PinballPVPContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext checkContext, CancellationToken ct = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
