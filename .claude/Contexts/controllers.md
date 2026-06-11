# Controller conventions (`Controllers/`)

- All controllers are versioned under `api/v{version:apiVersion}/[controller]` using `Asp.Versioning.Mvc`.
  Every controller carries `[ApiVersion(1)]` and `[Route("api/v{version:apiVersion}/[controller]")]`.
  `PlayerRecordsController` uses the explicit route `api/v{version:apiVersion}/users/playerrecords`.
  `LeaderboardsController` resolves to `api/v1/leaderboards`.
- `AssumeDefaultVersionWhenUnspecified = true` and `DefaultApiVersion = new ApiVersion(1)` are set, so
  unversioned requests are treated as v1 (useful during development).
- Controllers inject dependencies via primary constructors.
- Read endpoints use `.AsNoTracking()` and project straight to response DTOs with `.Select(Dto.Projection)`.
- Write endpoints validate referenced entities exist and uniqueness constraints up front (returning
  `BadRequest`/`NotFound`/`Forbid` with a message), then construct the entity, mutate any related aggregate
  state (e.g. `PlayerRecord` win/loss counts and highscores), `SaveChangesAsync`, and return
  `CreatedAtAction(nameof(Get...), ..., Dto.FromEntity(entity))`.

**Service layer migration:** all six controllers (`UsersController`, `PlayerRecordsController`,
`AuthController`, `LeaderboardsController`, `SoloMatchesController`, `VersusMatchesController`) no longer
talk to `PinballPVPContext` directly — they inject `IUserService` / `IPlayerRecordService` / `IAuthService`
/ `ILeaderboardService` / `ISoloMatchService` / `IVersusMatchService` (`Services/Users/`,
`Services/PlayerRecords/`, `Services/Auth/`, `Services/Leaderboards/`, `Services/SoloMatches/`,
`Services/VersusMatches/`) and switch on the `Result.Error`/error-enum values (or `null` for not-found
reads) returned by the service to pick the response. See [services.md](services.md) for the pattern; the
bullets above and the "Leaderboards" and "Versus match dual-confirmation flow" sections below still describe
the *logic* (now living in the services), just not where it physically sits for these controllers.

## Period filtering

`SoloMatchesController` and `VersusMatchesController` both support an optional `?period=week|month|year`
query filter on `PlayedAt`. The shared logic lives in `Extensions/PeriodFilterExtensions.cs`:

- `string?.IsValidPeriod()` — validates the parameter.
- `IQueryable<SoloMatch>.ApplyPeriodFilter(period)` and `IQueryable<VersusMatch>.ApplyPeriodFilter(period)`
  — typed overloads delegate to a private `GetPeriodRange` helper that computes the date range once, then
  apply concrete-type lambdas so EF Core can always translate the `PlayedAt` member access to SQL.

## Pagination

All list endpoints (`GetMatches`, `GetUserMatches`, `GetUsers`, and both leaderboard endpoints) accept
`?page` (default `1`) and `?pageSize` (default `20`, max `100`, enforced via `[Range(1, 100)]`).
The `ToPagedResultAsync<T>` extension in `Extensions/QueryableExtensions.cs` runs a `CountAsync` then a
`Skip`/`Take`/`ToListAsync` on the same `IQueryable` and returns a `PagedResult<T>` (defined in
`Dtos/PagedResult.cs`) with `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`,
`hasPreviousPage`.

## Leaderboards (`LeaderboardsController`)

Six paginated endpoints — three per match type — each accepting the same optional `?period=week|month|year`
filter as the match endpoints. Each response entry includes the full stat set for that mode
(`rank`, `userId`, `nickname`, `highscore`, `wins`, `losses`, `winRate`), regardless of sort category.

| Route | Sort key |
| ----- | -------- |
| `GET /api/v1/leaderboards/solo/highscore` | `Highscore` desc |
| `GET /api/v1/leaderboards/solo/wins` | `Wins` desc |
| `GET /api/v1/leaderboards/solo/winrate` | `Wins / (Wins + Losses) * 100` desc |
| `GET /api/v1/leaderboards/versus/highscore` | `Highscore` desc |
| `GET /api/v1/leaderboards/versus/wins` | `Wins` desc |
| `GET /api/v1/leaderboards/versus/winrate` | `Wins / (Wins + Losses) * 100` desc |

**`GET /api/v1/leaderboards/player/{userId}?period=`** returns a single `PlayerRankDto` with all six
ranks for that player in one call — intended for an in-game "your standings" screen. The response
contains a `solo` and a `versus` section; either is `null` if the player has no matches in that mode
within the selected period. Each section includes the player's stats plus three rank fields
(`highscoreRank`, `winsRank`, `winRateRank`). Returns `404` if the user doesn't exist.

**WinRate minimum matches threshold:** `Leaderboard:WinRateMinMatches` (default `10`) excludes players
with `Wins + Losses < threshold` from the `solo/winrate` and `versus/winrate` leaderboards and from the
`byWinRate` ranking list used for `winRateRank` — otherwise a single win (100%) would top the board over
established players. It does **not** affect the `highscore`/`wins` leaderboards or `highscoreRank`/`winsRank`.
A player below the threshold gets `winRateRank: 0` (their `winRate` value is still returned). The test
host (`PinballApiFactory`) sets this to `3` so integration tests don't need ten matches per player.

**Data source:** leaderboards aggregate directly from `SoloMatches` / `VersusMatches` (not `PlayerRecord`)
so the period filter is applied correctly. Players with no matches in the selected window don't appear,
which also guarantees `Wins + Losses > 0` — division-by-zero is structurally impossible.

**Aggregation helpers** (all in `LeaderboardService`, see [services.md](services.md)):
`GetSoloStatsAsync(period)` and `GetVersusStatsAsync(period)` are private methods that perform the DB
queries and return `List<LeaderboardStats>` (one shared record for both match types). Both the paginated
methods and `GetPlayerRankAsync` call these helpers — the paginated path then sorts and slices, the rank
path sorts three ways per mode (via `ApplySort`) and locates the player by `FindIndex`.

**Solo aggregation:** single `GroupBy(m => new { m.UserId, m.User.Nickname })` on `SoloMatches` with
`Max(FinalScore)`, `Count(HasWon)`, `Count(!HasWon)`. Translated to SQL via EF Core.

**Versus aggregation:** each match contributes to two players. Two separate `GroupBy` queries are run
(one grouping by `WinnerId`, one by `LoserId`) then merged in memory by `UserId`. Nicknames and
highscores come from the respective winner/loser columns.

**Sorting and pagination** happen in memory after the DB fetch (the full filtered set is loaded,
sorted, then `Skip`/`Take` applied). Rank is `(page - 1) * pageSize + index + 1`.

`WinRate` is `Math.Round(wins / (wins + losses) * 100, 2)` — computed in memory from fetched integers.
The private `ApplySort(stats, LeaderboardSortBy)` helper switches on the `LeaderboardSortBy` enum
(`Highscore`/`Wins`/`WinRate`) so the two paginated service methods and the rank computation share one
sorting implementation.

## Yearly leaderboards and all-time bests

`GET /api/v1/users/playerrecords/{id}` (`PlayerRecordsController`) nests an `allTimeBest` object
(`AllTimeBestRecordDto`) in its response — the continuously-maintained personal bests from
`AllTimeBestRecord`, each value paired with the year it was achieved (nullable if never set). Both
`PlayerRecordResponseDto.Projection` and `FromEntity` walk the `User.AllTimeBestRecord` navigation (EF
Core translates the nested-object projection into a join, no `.Include()` needed).

**`GET /api/v1/leaderboards/yearly/{year}`** returns a `YearlyLeaderboardResponseDto` with six
`List<YearlyLeaderboardEntryDto>` (one per `YearlyLeaderboardCategory`), each already ordered by `Rank`.
Returns `404` if no `YearlyLeaderboardEntry` rows exist for that year (rollover hasn't processed it yet,
or the year is out of range). See [persistence.md](persistence.md) for how entries are populated.

## Versus match dual-confirmation flow (`VersusMatchService.CreateMatchAsync`)

`POST /api/v1/versusmatches` enforces dual confirmation to prevent one-sided result forgery. The
`dto.WinnerId == dto.LoserId` and `User.GetUserId()` participant checks happen in
`VersusMatchesController` before calling the service (see [services.md](services.md)); everything below
is `VersusMatchService.CreateMatchAsync(reporterId, dto)`:

1. **First reporter** (either participant): a `PendingVersusMatch` row is created and `202 Accepted`
   returned. The row expires after `PendingVersusMatch.ConfirmationWindowMinutes` (5 min) to handle
   disconnected/crashed sessions.
2. **Second reporter** (the other participant): their submission is compared field-for-field against the
   pending row.
   - **Exact match** → pending row deleted, `VersusMatch` committed, `PlayerRecord` aggregates updated,
     `201 Created` returned.
   - **Any mismatch** → pending row deleted, both submissions discarded, `409 Conflict` returned.
3. **Same player resubmits** → `400 Bad Request` (already waiting for opponent).
4. **Expired pending match** → treated as if no pending match exists (deleted inline, first-reporter path
   runs again).
5. **Concurrent first reports** (race) → unique index on `(MinPlayerId, MaxPlayerId)` fires; the loser of
   the race receives `409 Conflict` telling them to retry as the second reporter.

The `MinPlayerId`/`MaxPlayerId` columns are always `Math.Min/Max(WinnerId, LoserId)` — they normalise the
player pair so the unique constraint holds regardless of who each reporter claims won.
