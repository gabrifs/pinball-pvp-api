# Persistence configuration

- `Program.cs` wires up `AddDbContext<PinballPVPContext>` with Npgsql + snake_case naming, controllers,
  validation, CORS, JWT authentication/authorization, rate limiting, application services, Serilog, and
  Swagger (Swagger UI only in `Development`). Middleware order matters:
  `UseExceptionHandler()` runs first (catches anything that escapes later middleware);
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
  default 24 h) and bulk-deletes expired `RefreshToken` and `PendingVersusMatch` rows via
  `ExecuteDeleteAsync`. It creates its own `IServiceScope` per run since `DbContext` is scoped and
  `BackgroundService` is singleton.
