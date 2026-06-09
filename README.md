# PinballPVP API

A REST API backing a Unity **head-to-head pinball game**, built with **ASP.NET Core (.NET 10)** and
**PostgreSQL**. It handles player accounts and authentication, tracks solo (vs CPU) and versus (P2P
player-vs-player) matches, and maintains an aggregated player record (wins, losses and highscores) for
each player — the foundation for the in-game leaderboards.

> Looking for what's left before this is production-ready? See [TODO.md](TODO.md).

## Features

- **User accounts & authentication** — registration with unique usernames, nicknames and emails,
  Argon2-hashed passwords, and JWT bearer login.
- **Solo matches** — log a single-player session (final score, rounds won, win/loss) against your own highscore.
- **Versus matches** — dual-confirmation P2P results: both participants must independently submit
  matching scores before a match is recorded, preventing one-sided forgery.
- **Player records** — aggregated solo/versus win-loss counts and highscores per user.
- **Leaderboards** — paginated rankings for solo and versus modes, each with three categories:
  highscore, raw wins, and win rate (`wins / (wins + losses) × 100`, rounded to 2 dp). Supports the
  same `?period=week|month|year` filter as match endpoints — omit for all-time. Only players who have
  played at least one match in the tracked mode and period appear.
- **Period filters** — list matches filtered by `week`, `month` or `year`.
- **Pagination** — all list endpoints accept `?page` and `?pageSize` (max 100); responses include
  `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`.
- **API versioning** — all routes are under `/api/v1/`; the contract is versioned so shipped game
  clients are never broken by future changes.
- **Swagger / OpenAPI** UI for exploring and testing the API in development.

## Tech stack

- [ASP.NET Core 10](https://learn.microsoft.com/aspnet/core) Web API
- [Entity Framework Core](https://learn.microsoft.com/ef/core) with [Npgsql](https://www.npgsql.org/efcore/) (PostgreSQL provider)
- [EFCore.NamingConventions](https://github.com/efcore/EFCore.NamingConventions) for snake_case database naming
- [Isopoh.Cryptography.Argon2](https://github.com/mheyman/Isopoh.Cryptography.Argon2) for password hashing
- JWT bearer authentication via `Microsoft.AspNetCore.Authentication.JwtBearer`
- [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) for Swagger/OpenAPI generation

## Project structure

```text
PinballPVP.Api/
├── Controllers/   # API endpoints (Auth, Users, SoloMatches, VersusMatches, PlayerRecords, Leaderboards)
├── Models/        # EF Core entities (User, SoloMatch, VersusMatch, PlayerRecord,
│                  #   RefreshToken, PendingVersusMatch)
├── Dtos/          # Request/response DTOs, grouped by feature (User, Login, Matches,
│                  #   Player Records, Leaderboards); includes shared PagedResult<T> wrapper
├── Data/          # PinballPVPContext (EF Core DbContext) and entity configuration
├── Services/      # Application services (password hashing, JWT issuing, refresh tokens,
│                  #   global exception handler, health check, background maintenance)
├── Middleware/    # Request pipeline middleware (CorrelationIdMiddleware)
├── Extensions/    # Helper extensions (JWT claims, period filtering, paginated queries)
└── Migrations/    # EF Core database migrations
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A running [PostgreSQL](https://www.postgresql.org/) instance

### Setup

1. Clone the repository and restore dependencies:

   ```bash
   dotnet restore
   ```

2. Configure your database connection string and JWT signing key as [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
   — `appsettings.Development.json` intentionally ships with these blank so nothing sensitive is committed:

   ```bash
   cd PinballPVP.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=pinballpvp;Username=<user>;Password=<password>"
   dotnet user-secrets set "Jwt:Key" "<a long random signing secret>"
   ```

   The non-sensitive `Jwt` settings (`Issuer`, `Audience`, `ExpirationMinutes`) and the CORS
   `AllowedOrigins` list still live in `appsettings.Development.json` and don't need to be overridden
   locally. For **production**, set allowed origins via environment variables:

   ```env
   Cors__AllowedOrigins__0=https://your-webgl-host.example.com
   ```

   The `appsettings.json` ships with an empty origins list, so CORS is disabled until explicitly
   configured — this is intentional.

3. Apply the database migrations:

   ```bash
   dotnet ef database update --project PinballPVP.Api
   ```

4. Run the API:

   ```bash
   dotnet run --project PinballPVP.Api
   ```

   Or with hot reload during development:

   ```bash
   dotnet watch run --project PinballPVP.Api
   ```

5. Browse the Swagger UI (development only) at `/swagger` — e.g. `http://localhost:5044/swagger`.

## API overview

All endpoints are rooted at `/api/v1`. Routes marked 🔒 require a JWT bearer token (`Authorization: Bearer <token>`),
obtained via `POST /api/v1/auth`.

| Method | Route                                   | Auth | Description                                                 |
|--------|-----------------------------------------|:----:|-------------------------------------------------------------|
| POST   | `/api/v1/auth`                          |      | Log in — returns a JWT access token and a refresh token     |
| POST   | `/api/v1/auth/refresh`                  |      | Exchange a refresh token for a new access + refresh pair    |
| POST   | `/api/v1/auth/logout`                   |  🔒  | Revoke the supplied refresh token                           |
| GET    | `/api/v1/users`                         |      | List users (paginated)                                      |
| GET    | `/api/v1/users/{id}`                    |      | Get a single user                                           |
| POST   | `/api/v1/users`                         |      | Register a new user                                         |
| GET    | `/api/v1/users/playerrecords/{id}`      |      | Get a user's aggregated player record                       |
| GET    | `/api/v1/solomatches`                   |      | List solo matches (paginated, optional `?period`)           |
| GET    | `/api/v1/solomatches/{id}`              |      | Get a single solo match                                     |
| GET    | `/api/v1/solomatches/user/{userId}`     |      | List a user's solo matches (paginated, optional `?period`)  |
| POST   | `/api/v1/solomatches`                   |  🔒  | Log a new solo match (caller must be the match's player)    |
| GET    | `/api/v1/versusmatches`                 |      | List versus matches (paginated, optional `?period`)         |
| GET    | `/api/v1/versusmatches/{id}`            |      | Get a single versus match                                   |
| GET    | `/api/v1/versusmatches/user/{userId}`   |      | List a user's versus matches (paginated, optional `?period`)|
| POST   | `/api/v1/versusmatches`                 |  🔒  | Submit a versus match result — see dual-confirmation below  |
| GET    | `/api/v1/leaderboards/solo/highscore`   |      | Solo leaderboard ranked by highscore (paginated, `?period`) |
| GET    | `/api/v1/leaderboards/solo/wins`        |      | Solo leaderboard ranked by wins (paginated, `?period`)      |
| GET    | `/api/v1/leaderboards/solo/winrate`     |      | Solo leaderboard ranked by win rate (paginated, `?period`)  |
| GET    | `/api/v1/leaderboards/versus/highscore` |      | Versus leaderboard by highscore (paginated, `?period`)      |
| GET    | `/api/v1/leaderboards/versus/wins`      |      | Versus leaderboard by wins (paginated, `?period`)           |
| GET    | `/api/v1/leaderboards/versus/winrate`   |      | Versus leaderboard by win rate (paginated, `?period`)       |
| GET    | `/health`                               |      | Health check — reports database connectivity status         |

### Authentication

`POST /api/v1/auth` accepts `{ "username": "...", "password": "..." }` and returns
`{ "token": "<jwt>", "refreshToken": "<opaque>" }` on success. Send the JWT as
`Authorization: Bearer <token>` on subsequent 🔒 requests.

When the access token expires, call `POST /api/v1/auth/refresh` with `{ "refreshToken": "..." }` to receive a
new token pair — the old refresh token is revoked and a new one issued (rotation). To log out, call
`POST /api/v1/auth/logout` (requires the current access token) with `{ "refreshToken": "..." }` to revoke the
refresh token; the endpoint is idempotent.

The API identifies the caller from the JWT's `sub` claim (the user's id) — protected match-creation
endpoints verify the authenticated user is one of the players named in the request body and respond
`403 Forbidden` otherwise.

### Pagination

All list endpoints (`GET /api/v1/users`, `/api/v1/solomatches`, `/api/v1/versusmatches`, and their
`/user/{userId}` variants, plus both leaderboard endpoints) accept `?page` (default `1`) and
`?pageSize` (default `20`, max `100`) query parameters. Responses use a consistent envelope:

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 4820,
  "totalPages": 241,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### Versus match confirmation

`POST /api/versusmatches` uses a dual-confirmation flow to prevent one player from unilaterally
falsifying a result:

1. **First reporter** (either participant) submits the result → `202 Accepted`. The submission is held
   as a pending match for up to 5 minutes.
2. **Second reporter** (the other participant) submits their version:
   - If all fields agree exactly → `201 Created`, match recorded, player records updated.
   - If any field differs → `409 Conflict`, both submissions discarded. Neither player gains anything.
3. If no confirmation arrives within 5 minutes (e.g. the other player crashed), the pending match
   expires and the first reporter can submit again to start a fresh confirmation window.

### Rate limiting

`POST /api/auth` (login) and `POST /api/users` (registration) are unauthenticated and abuse-prone, so both
share a sliding-window rate limit per client IP (5 requests/minute). Exceeding it returns
`429 Too Many Requests` with a `Retry-After` header indicating how long to wait before retrying.

### Observability

All request logs are written via **Serilog**. In development, logs are human-readable console output; in
production the default configuration emits compact JSON suitable for log aggregators (Seq, Datadog, etc.).

Every HTTP response includes an `X-Correlation-ID` header — a unique ID for that request. If a client
reports an error, that ID can be used to grep logs and retrieve the complete trace for the offending request.
Clients may also send their own `X-Correlation-ID` on the way in to propagate a cross-service trace ID.

`GET /health` returns `200 OK` when the API and its database connection are healthy, `503 Service
Unavailable` otherwise — suitable for container orchestration liveness/readiness probes and uptime monitors.

## Database migrations

Migrations are managed with the EF Core CLI tools (`dotnet ef`):

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project PinballPVP.Api

# Apply pending migrations to the database
dotnet ef database update --project PinballPVP.Api
```
