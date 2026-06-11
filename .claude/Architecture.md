# Architecture

## Layering

Requests flow `Controller -> Service -> PinballPVPContext (EF Core DbContext) -> PostgreSQL`. Business logic
(validation, persistence, aggregate updates) lives in injectable per-feature services under
`Services/<Feature>/` — see [Contexts/services.md](Contexts/services.md) for the structure and
conventions. All six controllers (`UsersController`, `PlayerRecordsController`, `AuthController`,
`LeaderboardsController`, `SoloMatchesController`, `VersusMatchesController`) have been migrated to this
pattern; `UsersController` / `Services/Users/` was the first and remains the template for new services.
There is no separate repository layer — services talk to the `DbContext` directly. Cross-cutting concerns
that don't belong in a controller (password hashing, JWT issuing) are also extracted into `Services/` and
injected via DI. DTOs live in `Dtos/` and are the boundary between EF entities (`Models/`) and the wire
format; entities are never returned directly from endpoints.

## Feature-specific conventions (`.claude/Contexts/`)

The detailed, per-area conventions below used to live inline here — they've been split into standalone files
under `.claude/Contexts/` so this file stays manageable as the project grows. They are **not** auto-loaded;
read the relevant one before working in that area, and add new ones here following the same pattern as the
codebase gains features:

- [Contexts/services.md](Contexts/services.md) — the per-feature service layer structure (interface +
  implementation) and the `Result`-record pattern used for write operations, with `Services/Users/` as
  the template for migrating the remaining controllers.
- [Contexts/entities.md](Contexts/entities.md) — `Models/` entity relationships and how
  `PlayerRecord` aggregates are maintained as a side effect of match creation.
- [Contexts/dtos.md](Contexts/dtos.md) — the `Projection`/`FromEntity` convention every response
  DTO follows, and how request DTOs are validated.
- [Contexts/auth.md](Contexts/auth.md) — JWT authentication setup and gotchas, the rate limiting
  policy on `/api/auth` and `/api/users`, and the "caller must be a named participant" authorization pattern
  for match creation.
- [Contexts/controllers.md](Contexts/controllers.md) — routing, DI, read/write endpoint
  conventions, and the duplicated period-filter helpers worth consolidating.
- [Contexts/persistence.md](Contexts/persistence.md) — what `Program.cs` wires up, middleware
  ordering, and the server-side `PlayedAt` timestamp rule.
