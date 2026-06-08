# Persistence configuration

- `Program.cs` wires up `AddDbContext<PinballPVPContext>` with Npgsql + snake_case naming, controllers,
  validation, JWT authentication/authorization, rate limiting, application services, and Swagger (Swagger UI
  only in `Development`). Middleware order matters: `UseRateLimiter()` runs before `UseAuthentication()` (so
  throttled requests are rejected before spending CPU on JWT validation), and `UseAuthentication()` must run
  before `UseAuthorization()`.
- Match timestamps (`PlayedAt`) are always set server-side via `DateTime.UtcNow` on creation, never taken
  from the client DTO.
