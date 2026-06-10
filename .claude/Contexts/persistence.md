# Persistence configuration

- `Program.cs` wires up `AddDbContext<PinballPVPContext>` with Npgsql + snake_case naming, controllers,
  validation, CORS, JWT authentication/authorization, rate limiting, application services, Serilog, and
  Swagger (Swagger UI only in `Development`). Middleware order matters:
  `UseExceptionHandler()` runs first (catches anything that escapes later middleware);
  `UseHttpsRedirection()` runs next — in the production Docker image (HTTP-only port 8080, no HTTPS port
  configured, no `UseForwardedHeaders()`), ASP.NET Core can't determine an HTTPS port, so this logs a
  one-time warning and passes requests through unchanged rather than redirecting; it's only meaningful
  for hosting where both HTTP and HTTPS ports are bound directly to the app (e.g. the local `https`
  launch profile). Revisit once a hosting target with TLS termination is chosen — see
  [TODO.md](../../TODO.md);
  `UseMiddleware<CorrelationIdMiddleware>()` runs next (assigns `X-Correlation-ID` so it's in scope for all
  subsequent logs); `UseSerilogRequestLogging()` runs after that (logs each request with timing + correlation
  ID); `UseCors()` runs before `UseRateLimiter()` (preflight OPTIONS handled before rate limiter);
  `UseRateLimiter()` runs before `UseAuthentication()` (throttled requests rejected before JWT validation);
  `UseAuthentication()` must run before `UseAuthorization()`.
- CORS allowed origins are configured via `Cors:AllowedOrigins` in `appsettings.json` (empty by default —
  CORS is disabled unless origins are explicitly listed). Add origins for each environment via
  `appsettings.<Env>.json` or environment variables (`Cors__AllowedOrigins__0=...`). CORS only affects
  browser clients (Unity WebGL builds); desktop/mobile Unity builds are not subject to CORS.
- Serilog is configured via the `Serilog:` section in `appsettings.json` (compact JSON output, suitable for
  log aggregators) with a development override in `appsettings.Development.json` (human-readable template).
  `CorrelationIdMiddleware` (`Middleware/`) reads or generates an `X-Correlation-ID` per request, pushes it
  into Serilog's `LogContext`, and echoes it in the response — every log entry for that request carries the
  ID automatically. Serilog replaces the default `Logging:` configuration; that section is no longer present
  in `appsettings*.json`.
- `GET /health` is mapped via `MapHealthChecks("/health")`. `DatabaseHealthCheck` (`Services/Health/`)
  calls `dbContext.Database.CanConnectAsync()` — returns `Healthy` or `Unhealthy` depending on whether
  Postgres is reachable. No extra NuGet packages needed; `IHealthCheck` ships with ASP.NET Core.
- `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 30 s)` is configured on the Npgsql provider so
  transient Postgres blips are retried transparently before surfacing as request failures.
- `GlobalExceptionHandler` (`Services/ExceptionHandling/`) implements `IExceptionHandler` — any unhandled
  exception is caught, logged with method + path, and returned to the client as a `ProblemDetails` JSON body
  (RFC 9457) with `500 Internal Server Error`. `AddProblemDetails()` ensures framework-produced 4xx
  responses (404, 405, etc.) also use the `ProblemDetails` shape.
- Match timestamps (`PlayedAt`) are always set server-side via `DateTime.UtcNow` on creation, never taken
  from the client DTO.
- `ExpiredRecordPurgeService` (`Services/Maintenance/`) is a `BackgroundService` registered as a hosted
  service. It runs once on startup then on a configurable interval (`Maintenance:PurgeIntervalHours`,
  default 24 h) and bulk-deletes expired `RefreshToken` rows, expired `PendingVersusMatch` rows, and
  used/expired `PasswordRecoveryCode` rows via `ExecuteDeleteAsync`. It creates its own `IServiceScope`
  per run since `DbContext` is scoped and `BackgroundService` is singleton. Each run also resolves
  `IYearRolloverService` from the same scope and calls `ProcessAsync` (see below).
- `YearRolloverService` (`Services/Maintenance/`, `IYearRolloverService`) performs the yearly data
  rollover. It's kept as its own scoped service (SOLID/unit-testable in isolation) but invoked from
  `ExpiredRecordPurgeService` every cycle rather than on its own schedule. `ProcessAsync` finds every
  distinct year with `SoloMatch`/`VersusMatch` rows dated before `Jan 1` of the current year (UTC,
  via `PeriodFilterExtensions.GetYearRange`) and processes them in chronological order. For each prior
  year, the work runs inside a transaction via `context.Database.CreateExecutionStrategy().ExecuteAsync(...)`
  — required because `EnableRetryOnFailure` forbids ad-hoc `BeginTransactionAsync` outside an execution
  strategy:
  1. Snapshot every `PlayerRecord` (joined to `User.Nickname`, `AsNoTracking`) and compute the top 3 per
     `YearlyLeaderboardCategory`. Highscore/Wins categories require `Wins + Losses > 0` (mirrors the
     live leaderboards' "no matches → not listed" guarantee); WinRate categories require
     `Wins + Losses >= Leaderboard:WinRateMinMatches` (default 10), with
     `Value = Math.Round(Wins / (Wins + Losses) * 100, 2)`. Insert the resulting `YearlyLeaderboardEntry`
     rows.
  2. Reset all `PlayerRecord` aggregate fields to zero via `ExecuteUpdateAsync` — from this point
     `PlayerRecord` is year-to-date only (see [entities.md](entities.md)). `AllTimeBestRecord` is
     untouched (it's maintained separately, on every match).
  3. Bulk-delete that year's `SoloMatch`/`VersusMatch` rows via `ExecuteDeleteAsync`, scoped with
     `PeriodFilterExtensions.GetYearRange(year)`.

  **Multi-year backlog**: if the service has been down across more than one New Year's boundary, only the
  first processed prior year produces a meaningful snapshot — by the time later years are processed,
  `PlayerRecord` has already been zeroed, so their `qualifies` filters yield no rows (no
  `YearlyLeaderboardEntry` rows are inserted for them, but their matches are still pruned). This is a
  graceful no-op, acceptable given the service runs at least daily by default.
