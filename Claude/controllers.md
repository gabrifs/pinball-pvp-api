# Controller conventions (`Controllers/`)

- Routed at `api/[controller]` (e.g. `api/users`, `api/solomatches`, `api/versusmatches`, `api/auth`);
  `PlayerRecordsController` is nested under `api/users/[controller]`.
- Controllers inject dependencies (`PinballPVPContext`, and services like `IPasswordHasher`/`IJwtTokenService`
  where needed) via primary constructors.
- Read endpoints use `.AsNoTracking()` and project straight to response DTOs with `.Select(Dto.Projection)`.
- Write endpoints validate referenced entities exist and uniqueness constraints up front (returning
  `BadRequest`/`NotFound`/`Forbid` with a message), then construct the entity, mutate any related aggregate
  state (e.g. `PlayerRecord` win/loss counts and highscores), `SaveChangesAsync`, and return
  `CreatedAtAction(nameof(Get...), ..., Dto.FromEntity(entity))`.
- `SoloMatchesController` and `VersusMatchesController` both implement `IsValidPeriod` / `ApplyPeriodFilter`
  helpers supporting an optional `?period=week|month|year` query filter on `PlayedAt`. These two
  implementations are currently duplicated near-verbatim across the two controllers — when touching this
  logic, consider whether it should be consolidated (per the S.O.L.I.D./clean-code directive in the root
  [CLAUDE.md](../CLAUDE.md)) rather than further duplicated into new controllers.
- `PlayerRecordsController` currently only implements `GetPlayerRecord`.

## Versus match dual-confirmation flow (`VersusMatchesController.CreateMatch`)

`POST /api/versusmatches` enforces dual confirmation to prevent one-sided result forgery:

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
