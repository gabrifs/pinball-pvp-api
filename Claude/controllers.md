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
