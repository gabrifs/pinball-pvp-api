using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using PinballPVP.Api.Data;
using PinballPVP.Api.Services.Email;
using PinballPVP.Api.Services.RateLimiting;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace PinballPVP.Tests.Infrastructure;

public class PinballApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("pinballpvp_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    private Respawner _respawner = default!;

    public FakeEmailService EmailService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Jwt:Key"] = "test-signing-key-for-pinball-pvp-tests-must-be-long-enough!",
                ["Jwt:Issuer"] = "PinballPVP.Api",
                ["Jwt:Audience"] = "PinballPVP.Client",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "30",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the production rate-limiter policy so tests aren't throttled.
            // All rate-limited endpoints share a 5 req/min limit per IP, which is hit
            // immediately when many tests run against the same in-process server.
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.AddRateLimiter(options =>
                options.AddPolicy(RateLimiterPolicyNames.AuthEndpoints, _ =>
                    RateLimitPartition.GetNoLimiter(string.Empty)));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PinballPVPContext>();
        await db.Database.MigrateAsync();

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
