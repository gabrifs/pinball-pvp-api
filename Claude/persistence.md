# Persistence configuration

- `Program.cs` wires up `AddDbContext<PinballPVPContext>` with Npgsql + snake_case naming, controllers,
  validation, CORS, JWT authentication/authorization, rate limiting, application services, and Swagger
  (Swagger UI only in `Development`). Middleware order matters: `UseCors()` runs before `UseRateLimiter()`
  (so preflight OPTIONS requests are handled before the rate limiter sees them), `UseRateLimiter()` runs
  before `UseAuthentication()` (throttled requests are rejected before spending CPU on JWT validation),
  and `UseAuthentication()` must run before `UseAuthorization()`.
- CORS allowed origins are configured via `Cors:AllowedOrigins` in `appsettings.json` (empty by default —
  CORS is disabled unless origins are explicitly listed). Add origins for each environment via
  `appsettings.<Env>.json` or environment variables (`Cors__AllowedOrigins__0=...`). CORS only affects
  browser clients (Unity WebGL builds); desktop/mobile Unity builds are not subject to CORS.
- Match timestamps (`PlayedAt`) are always set server-side via `DateTime.UtcNow` on creation, never taken
  from the client DTO.
- `ExpiredRecordPurgeService` (`Services/Maintenance/`) is a `BackgroundService` registered as a hosted
  service. It runs once on startup then on a configurable interval (`Maintenance:PurgeIntervalHours`,
  default 24 h) and bulk-deletes expired `RefreshToken` and `PendingVersusMatch` rows via
  `ExecuteDeleteAsync`. It creates its own `IServiceScope` per run since `DbContext` is scoped and
  `BackgroundService` is singleton.
