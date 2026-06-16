using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PinballPVP.Api.Data;

// Lets 'dotnet ef' commands and migration bundles create the context without running
// full application startup (which requires JWT keys, email config, etc.).
// Reads ConnectionStrings__DefaultConnection from environment variables or command-line args.
public class PinballPVPContextFactory : IDesignTimeDbContextFactory<PinballPVPContext>
{
    public PinballPVPContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var options = new DbContextOptionsBuilder<PinballPVPContext>()
            .UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PinballPVPContext(options);
    }
}
