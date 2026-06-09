# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

PinballPVP.Api is an ASP.NET Core Web API (.NET 10) backing a Unity head-to-head pinball game. It exposes
REST endpoints for user accounts/auth, solo matches (vs CPU), versus matches (P2P player vs player), and
aggregated player records, persisting to PostgreSQL via EF Core.

**This project serves a dual purpose: it's both a portfolio piece and the backend for a commercial game.**
That raises the bar on code quality, security, and production-readiness beyond what a typical hobby/learning
project would need — treat shortcuts and "good enough for now" choices with extra scrutiny, since both a
prospective employer/client and real paying players may end up depending on this code.

See [TODO.md](TODO.md) for the production-readiness roadmap (security hardening, observability, testing, etc.).

## Directives to always follow

- Strive to follow S.O.L.I.D. principles.
- Keep code clean and comprehensible — favor the existing conventions in this codebase over introducing new patterns.
- Always use the modern Gold Standard on all features (current language/framework idioms and best practices — e.g. .NET 10 / C# latest, EF Core current APIs — rather than outdated or legacy approaches).
- Update this file (CLAUDE.md) whenever a change is made that's worth documenting here — new architecture, conventions, gotchas, etc.
- Always update README.md when a change might affect what it documents (features, setup steps, API surface, project structure).
- Always update TODO.md when working on items related to it — remove items once they're completed (rather
  than leaving them checked off; TODO.md is a roadmap of what's left, and git history already documents
  what was done and why), and add newly-discovered items.
- **Never write secrets or sensitive data into any tracked file** — connection strings, signing/API keys,
  passwords, tokens, certificates, etc. must only ever live in `dotnet user-secrets` (local dev) or
  environment variables/a secrets manager (other environments); see [Claude/auth.md](Claude/auth.md) and the
  `user-secrets` note under [EF Core migrations](#ef-core-migrations). Once committed, a secret is in git
  history permanently (even if later removed from the working tree) — treat any value that reaches a commit
  as compromised and rotate it rather than relying on a follow-up commit to "remove" it.
- When a feature area grows enough conventions to need documenting, add a new file under `Claude/` rather
  than growing this file inline — see [Feature-specific conventions](#feature-specific-conventions-claude)
  below for the existing examples and the rationale.

## Commands

Run all commands from the repo root (`PinballPVP.slnx`) or from `PinballPVP.Api/`.

- Build: `dotnet build`
- Run (dev server with hot reload): `dotnet watch run --project PinballPVP.Api`
- Run (no watch): `dotnet run --project PinballPVP.Api`
- Run tests: `dotnet test` (requires Docker for Testcontainers)
- Swagger UI is available at `/swagger` when running in the `Development` environment (launch profiles use `http://localhost:5044` / `https://localhost:7240`).

### CI/CD

The GitHub Actions workflow lives at [`.github/workflows/ci.yml`](.github/workflows/ci.yml). It runs on
every push and PR to `master`:

- **build-and-test** — restores, builds, and runs the full test suite (`dotnet test`). Docker must be
  available on the runner because Testcontainers requires it; `ubuntu-latest` provides this automatically.
- **docker** (master pushes only, after build-and-test) — builds the production Docker image and pushes
  it to `ghcr.io/<owner>/<repo>` tagged `:latest` and `:sha-<short-sha>`. Uses `GITHUB_TOKEN` (no extra
  secrets needed). GitHub layer caching (`type=gha`) keeps rebuilds fast.

A deploy step is intentionally left as a comment placeholder — implement it once a hosting target is chosen.
When you add it, run `dotnet ef database update` (or an EF bundle) against the production DB **before**
rolling out the new container, not in the same step, to avoid downtime from a mid-migration restart.

### Docker

A multi-stage `Dockerfile` lives at the repo root. Build and run with:

```bash
docker build -t pinball-pvp-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  -e Jwt__Key="..." \
  pinball-pvp-api
```

Key points:

- The image listens on **HTTP port 8080** only — TLS termination belongs at the reverse proxy layer.
- `ASPNETCORE_ENVIRONMENT` defaults to `Production`; override with `-e ASPNETCORE_ENVIRONMENT=Development` if needed.
- **Migrations are not applied automatically on startup** — run them as a separate CI/CD step before deploying:
  `dotnet ef database update --project PinballPVP.Api` (with the production connection string set).
- All secrets (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Email__Password`, etc.) must be
  injected as environment variables — never bake them into the image.

### Testing

The test project is `PinballPVP.Tests/` and uses **xUnit** with **Testcontainers** + **Respawn**:

- **Testcontainers.PostgreSql** spins up a real Postgres container for the test run — Docker must be
  running. The container is started once for the entire test session (shared via `ICollectionFixture`)
  and torn down afterward.
- **Respawn** truncates all application tables before each test method, giving every test a clean slate
  without the overhead of recreating the schema.
- **`PinballApiFactory`** (`Tests/Infrastructure/`) is the `WebApplicationFactory<Program>` that wires
  the Testcontainer connection string and a `FakeEmailService` (captures recovery codes in-memory rather
  than sending real SMTP) into the running app.
- **`IntegrationTestBase`** is the base class for all integration test classes. Because xUnit creates a
  fresh class instance per test method, `InitializeAsync` (DB + email reset) runs before every individual
  test.
- **Unit tests** live under `Tests/Unit/` and cover pure logic (e.g., `PeriodFilterExtensions`) with no
  dependencies.
- **Integration tests** live under `Tests/Integration/` and exercise the full HTTP stack (auth, users,
  solo matches, versus matches).

### EF Core migrations

The DB context is `PinballPVP.Api.Data.PinballPVPContext`, mapped to PostgreSQL with snake_case column/table
naming (via `EFCore.NamingConventions` + `UseSnakeCaseNamingConvention()`).

- Add a migration: `dotnet ef migrations add <Name> --project PinballPVP.Api`
- Apply migrations to the DB: `dotnet ef database update --project PinballPVP.Api`
- The connection string key is `ConnectionStrings:DefaultConnection` (a local Postgres instance on port 5431,
  db `pinballpvp`, by convention).

> **Note:** `ConnectionStrings:DefaultConnection` and `Jwt:Key` are loaded from `dotnet user-secrets`
> (the project has a `UserSecretsId`) — `appsettings.Development.json` ships with these blank intentionally.
> Set them locally with `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."` and
> `dotnet user-secrets set "Jwt:Key" "..."` from the `PinballPVP.Api/` directory. Never put real secrets back
> into a checked-in `appsettings*.json` file.

## Architecture

### Layering

Requests flow `Controller -> PinballPVPContext (EF Core DbContext) -> PostgreSQL`. There is no separate
repository layer — controllers talk to the `DbContext` directly. Cross-cutting concerns that don't belong in
a controller (password hashing, JWT issuing) are extracted into `Services/` and injected via DI. DTOs live in
`Dtos/` and are the boundary between EF entities (`Models/`) and the wire format; entities are never returned
directly from endpoints.

### Feature-specific conventions (`Claude/`)

The detailed, per-area conventions below used to live inline here — they've been split into standalone files
under `Claude/` so this file stays manageable as the project grows. They are **not** auto-loaded; read the
relevant one before working in that area, and add new ones here following the same pattern as the codebase
gains features:

- [Claude/entities.md](Claude/entities.md) — `Models/` entity relationships and how `PlayerRecord` aggregates
  are maintained as a side effect of match creation.
- [Claude/dtos.md](Claude/dtos.md) — the `Projection`/`FromEntity` convention every response DTO follows, and
  how request DTOs are validated.
- [Claude/auth.md](Claude/auth.md) — JWT authentication setup and gotchas, the rate limiting policy on
  `/api/auth` and `/api/users`, and the "caller must be a named participant" authorization pattern for match
  creation.
- [Claude/controllers.md](Claude/controllers.md) — routing, DI, read/write endpoint conventions, and the
  duplicated period-filter helpers worth consolidating.
- [Claude/persistence.md](Claude/persistence.md) — what `Program.cs` wires up, middleware ordering, and the
  server-side `PlayedAt` timestamp rule.
