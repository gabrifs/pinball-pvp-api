# Service layer conventions (`Services/`)

Business logic is being progressively extracted from controllers into injectable per-feature services
(see "Split Controllers into smaller services" in [TODO.md](../../TODO.md)). `UsersController` /
`Services/Users/` is the first controller migrated — use it as the template for the next one.

`PlayerRecordsController` / `Services/PlayerRecords/` is the second — a minimal example of a
read-only service: a single `GetPlayerRecordAsync` method returning `PlayerRecordResponseDto?`
(`null` for not-found), with no `Result` records since it has no write operations.

`AuthController` / `IAuthService` (`AuthService`, in the existing `Services/Auth/` folder alongside
`IJwtTokenService`/`IRefreshTokenService`) is the third. It shows two variations on the pattern:

- Methods that return a DTO on success (`LoginAsync`, `RefreshAsync`) use the usual
  `record Result(TError Error, TDto? Value)` shape (`LoginResult`, `RefreshResult`).
- Methods with no payload (`LogoutAsync`, `ResetPasswordAsync`) skip the wrapper record entirely and
  just return the error enum directly (`Task<LogoutError>`, `Task<ResetPasswordError>`) — a `Result`
  record whose `Value` is always `null` adds nothing.
- `ForgotPasswordAsync` returns `Task` (no result at all): the controller always responds `200 OK`
  regardless of whether the user exists, so there's no error for the service to communicate.

`AuthService` takes over the `PinballPVPContext.Database.CreateExecutionStrategy()` /
`BeginTransactionAsync` pattern from `Login`/`Refresh` (see [auth.md](auth.md)) — this is
orchestration logic, not persistence-only, but it still belongs in the service per S.O.L.I.D.
(the controller shouldn't know about transactions).

`LeaderboardsController` / `Services/Leaderboards/` is the fourth — entirely read-only, like
`PlayerRecordsController`, but with a richer interface:

- The six paginated leaderboard endpoints collapse to two service methods,
  `GetSoloLeaderboardAsync`/`GetVersusLeaderboardAsync`, parameterised by a `LeaderboardSortBy`
  enum (`Highscore`/`Wins`/`WinRate`) instead of the `Func<IEnumerable<Stats>, IOrderedEnumerable<Stats>>`
  sort lambdas the controller used to build inline. The enum is defined in `ILeaderboardService.cs`
  alongside the interface, following the same "interface file owns its enums/result records" convention
  as `LoginError`/`RefreshError` in `IAuthService.cs`.
- The previously-separate private `SoloStats`/`VersusStats` records (identical shapes) were merged into
  one `LeaderboardStats` record so a single private `ApplySort` helper — switching on `LeaderboardSortBy`
  — can serve both leaderboard kinds and `GetPlayerRankAsync`'s rank computation.
- `GetYearlyLeaderboardAsync(year)` and `GetPlayerRankAsync(userId, period)` return `null` for the
  not-found cases (no entries for that year / no such user), which the controller maps to `NotFound()` —
  same convention as `PlayerRecordsController`.
- Period validation (`period.IsValidPeriod()`) stays in the controller as a guard clause, like the
  `[Range(...)]` attribute validation on `page`/`pageSize` — it's pure request-shape validation with no
  DB dependency, not business logic.
- **Period-filtered versus leaderboard** (`GetVersusTopFromMatchesAsync`) uses a raw SQL FULL OUTER
  JOIN CTE (`Database.SqlQueryRaw<LeaderboardStats>`) so ORDER BY and LIMIT are pushed to SQL rather
  than applied in memory after materialising all rows. LINQ cannot express FULL OUTER JOIN, so raw SQL
  is the only option here. `GetVersusStatsAsync` (two LINQ GROUP BY queries merged in memory) is kept
  separately for `GetPlayerRankAsync`, where a cap cannot apply — rank computation requires every
  player's stats. `PeriodFilterExtensions.GetPeriodRange` is public so the service can translate the
  period string to UTC date bounds for the raw SQL parameters.

`SoloMatchesController` / `Services/SoloMatches/` (fifth) and `VersusMatchesController` /
`Services/VersusMatches/` (sixth) complete the controller migration:

- Both `GetUserMatchesAsync(userId, ...)` methods return `null` when the user doesn't exist (same
  not-found convention as above), mapped to `NotFound()`. As with `LeaderboardsController.GetPlayerRank`,
  the controller's `period.IsValidPeriod()` guard clause now runs *before* the service call, so an
  invalid period on a non-existent user yields `400` rather than the pre-extraction `404` — a
  behaviour-order detail nobody depends on, traded for keeping the guard-clause convention consistent.
- `SoloMatchService.CreateMatchAsync` is a small `CreateSoloMatchResult`/`CreateSoloMatchError`
  (`None`/`UserNotFound`) pair, following the standard write-operation shape.
- `VersusMatchService.CreateMatchAsync(reporterId, dto)` is the most complex write operation in the
  codebase — the dual-confirmation flow described in [controllers.md](controllers.md). Its
  `CreateVersusMatchResult`/`CreateVersusMatchError` pair extends the usual shape with **two non-error
  success outcomes**: `None` (match committed, `201 Created`, `Match` populated) and `Pending` (first
  reporter, `202 Accepted`, no `Match`) — alongside `UsersNotFound`, `AlreadyPending`,
  `ResultsMismatch`, and `PendingConflict` (the unique-index race), each mapped to its own `400`/`409`
  response by the controller.
- Both `CreateMatchAsync` implementations deduplicate retried submissions within a 60-second window
  (`DeduplicationWindowSeconds = 60`). Before any state mutation, each service queries for an existing
  match with an identical payload (same `UserId`/`FinalScore`/`RoundsWon`/`HasWon` for solo; same six
  score fields for versus) created in the last 60 seconds — if found, it returns that match without
  creating a new one or touching `PlayerRecord`. For versus, the dedup sits in the second-reporter
  confirmation path and handles the realistic retry scenario where the confirmed response was lost in
  transit: the other player (now acting as second reporter for the re-submitted pending) finds the
  already-committed match and returns it cleanly. **Tests must use distinct payloads when submitting
  multiple legitimate matches in a short loop, to avoid being caught by the dedup check.**
- Two checks stay in `VersusMatchesController` rather than the service: `dto.WinnerId == dto.LoserId`
  (pure cross-field DTO validation, no DB dependency — same rationale as period validation) and the
  `User.GetUserId()` participant check that returns `Forbid()` (needs the `ClaimsPrincipal`, which
  services don't take a dependency on). The resolved `reporterId` is passed into
  `CreateMatchAsync` as a plain `int`.

## Structure

Each migrated feature gets its own folder under `Services/<Feature>/` (plural, matching the controller
name — e.g. `Services/Users/` for `UsersController`) containing exactly two files:

- `I<Feature>Service.cs` — the interface, plus any `Result` records/enums its write operations return
  (see below).
- `<Feature>Service.cs` — the implementation, taking over the `PinballPVPContext` and any other
  dependencies (e.g. `IPasswordHasher`) the controller previously held, injected via primary constructor.

Register the service as scoped in `Program.cs` alongside the other `AddScoped<I..., ...>()` calls.

**Naming note:** the folder/namespace is plural (`Services/Users`, not `Services/User`) to avoid a
namespace/type collision with the `Models.User` entity — a namespace and a type sharing the same
unqualified name (`User`) causes ambiguous-reference errors when both `Models` and the service namespace
are `using`'d in the same file.

## Read operations

Read methods return DTOs (or `PagedResult<Dto>`) directly — `null` for a not-found single-entity lookup,
which the controller maps to `NotFound()`. Same `.AsNoTracking()` + `.Select(Dto.Projection)` +
`ToPagedResultAsync` conventions as before, just moved into the service.

## Write operations — the Result pattern

Write methods that can fail for *expected* business reasons (uniqueness conflicts, not-found) don't
throw — they return a small `record Result(TError Error, TDto? Value)` per operation, e.g.
`CreateUserResult` / `UpdateNicknameResult` in `IUserService.cs`:

```csharp
public enum CreateUserError { None, UsernameInUse, NicknameInUse, EmailInUse, DuplicateDetails }

public record CreateUserResult(CreateUserError Error, UserResponseDto? User)
{
    public bool Succeeded => Error == CreateUserError.None;
    public static CreateUserResult Success(UserResponseDto user) => new(CreateUserError.None, user);
    public static CreateUserResult Failure(CreateUserError error) => new(error, null);
}
```

The controller switches on `result.Error` to pick the HTTP response (`BadRequest("...")`, `NotFound()`,
`CreatedAtAction(...)`, `Ok(result.Value)`) — the service owns *what* went wrong, the controller owns *how*
that's expressed over HTTP. Postgres unique-constraint violations (`DbUpdateException` /
`SqlState: "23505"`) caught during `SaveChangesAsync` are mapped to the same error enum by
`ConstraintName`, exactly as the pre-extraction controller code did.

## All-purpose `CancellationToken`

Following `IRefreshTokenService`'s precedent, every service method takes a trailing
`CancellationToken ct = default`, even though controller actions don't currently accept/forward one.
