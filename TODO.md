# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Maintenance

- [ ] **Purge expired refresh tokens.** Every login creates a new `RefreshToken` row; expired rows are never
      deleted automatically. Add a background job or scheduled task to delete rows where `ExpiresAt < UtcNow`
      to prevent unbounded table growth at scale.

## Reliability & observability

- [ ] **Add global exception handling** (`UseExceptionHandler` / `IExceptionHandler`) so unexpected errors
      return a consistent `ProblemDetails` body instead of raw 500s with stack traces.
- [ ] **Add structured logging** (e.g. Serilog) with request correlation IDs — `appsettings.json` only has the
      default console logger configuration today.
- [ ] **Add health check endpoints** (`/health`) covering the database connection — needed for container
      orchestration / load balancers / uptime monitoring.
- [ ] **Add DB connection resiliency** (`EnableRetryOnFailure`) so transient Postgres blips don't surface as
      request failures.

## Testing

- [ ] **Add a test project.** There are currently no automated tests in the solution — at minimum, cover the
      controller logic (auth checks, uniqueness validation, win/loss + highscore aggregation) and the
      period-filter helpers with unit tests, plus integration tests against a real (test) database.

## API design

- [ ] **Fix the `WinnerNickame`/`LoserNickame` typo in `VersusMatchResponseDto`.** Both the record
      properties and the `Projection`/`FromEntity` mappings are missing the 'n' (`Nickname` → `Nickame`).
      This is a public API contract field name, so it's cheap to fix now but becomes a breaking change
      once the Unity client integrates against it — worth doing before that happens.
- [ ] **Add validation attributes to `CreateSoloMatchDto`/`CreateVersusMatchDto`.** Unlike `CreateUserDto`/
      `LoginDto`, these have no `[Range]`/`[Required]` attributes on `FinalScore`, `RoundsWon`,
      `WinnerFinalScore`, etc. — a client can currently submit negative or nonsensical scores, which would
      corrupt highscores and win/loss aggregates (and, on a commercial leaderboard, be trivially exploitable).
- [ ] **Add pagination** to the list endpoints (`GET /api/users`, `GET /api/solomatches`,
      `GET /api/versusmatches`) — they currently load and return entire tables, which won't scale as match
      history grows.
- [ ] **Consolidate the duplicated period-filter logic.** `IsValidPeriod`/`ApplyPeriodFilter` are implemented
      nearly verbatim in both `SoloMatchesController` and `VersusMatchesController` — extract into a shared
      service or extension method.
- [ ] **Finish `PlayerRecordsController`** (currently only `GetPlayerRecord` exists) and **build out the
      Leaderboards feature** — `Dtos/Leaderboards/` exists as a folder but has no DTOs or controller yet, and
      it's the headline feature the in-game leaderboard display depends on.
- [ ] **Add API versioning** before the Unity client locks onto the current contract, so the API can evolve
      without breaking shipped game builds.

## Deployment

- [ ] **Containerize the app** (Dockerfile) for consistent builds and deployment.
- [ ] **Set up CI/CD** (build, test, run migrations, deploy) — no pipeline exists yet.
- [ ] **Document production configuration** (connection strings, JWT settings, allowed hosts/CORS origins) as
      environment variables, separate from the checked-in `appsettings.Development.json`.
