# Commands

Run all commands from the repo root (`PinballPVP.slnx`) or from `PinballPVP.Api/`.

- Build: `dotnet build`
- Run (dev server with hot reload): `dotnet watch run --project PinballPVP.Api`
- Run (no watch): `dotnet run --project PinballPVP.Api`
- Run tests: `dotnet test` (requires Docker for Testcontainers)
- Swagger UI is available at `/swagger` when running in the `Development` environment (launch profiles use `http://localhost:5044` / `https://localhost:7240`).

## CI/CD

The GitHub Actions workflow lives at [`.github/workflows/ci.yml`](../.github/workflows/ci.yml). It runs on
every push and PR to `master`:

- **build-and-test** — restores, builds, and runs the full test suite (`dotnet test`). Docker must be
  available on the runner because Testcontainers requires it; `ubuntu-latest` provides this automatically.
- **docker** (master pushes only, after build-and-test) — builds the production Docker image and pushes
  it to `ghcr.io/<owner>/<repo>` tagged `:latest` and `:sha-<short-sha>`. Uses `GITHUB_TOKEN` (no extra
  secrets needed). GitHub layer caching (`type=gha`) keeps rebuilds fast.

A deploy step is intentionally left as a comment placeholder — implement it once a hosting target is chosen.
When you add it, run `dotnet ef database update` (or an EF bundle) against the production DB **before**
rolling out the new container, not in the same step, to avoid downtime from a mid-migration restart.

## Docker

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

## Testing

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

## EF Core migrations

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
