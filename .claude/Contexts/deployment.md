# Deployment

## Architecture

Self-hosted GitHub Actions runner on a Windows PC running Docker Desktop. On every push to `master`:

1. **build-and-test** (ubuntu-latest) — runs the full test suite.
2. **docker** (ubuntu-latest) — builds and pushes the image to GHCR as `:latest` and `:sha-<short-sha>`.
3. **deploy** (self-hosted) — pulls the new image, runs migrations, rolls out the API.

## Files

- **`Dockerfile`** — multi-stage build: publishes the API and builds an EF Core migration bundle
  (`efbundle`) in the same stage. Both end up in the runtime image at `/app/`. The `migrate` service
  in docker-compose overrides the entrypoint to `["./efbundle"]`.
- **`docker-compose.yml`** — three services:
  - `db` — Postgres 17 with a named volume (`pgdata`) and a `pg_isready` healthcheck; not exposed to the host.
  - `migrate` — one-off service (`restart: "no"`) that runs `efbundle`, applying pending migrations. Depends on `db` being healthy.
  - `api` — the app, published on port 8080. Depends on `migrate` completing successfully (`service_completed_successfully`).
- **`.env.example`** — template for the secrets file that lives on the host outside the git checkout.
  Copy to the path stored in the `DEPLOY_ENV_FILE` repository variable and fill in real values.
- **`.gitignore`** — `.env` is ignored; only `.env.example` is tracked.

## Variable split: host `.env` vs. CI

`IMAGE_TAG` and `GITHUB_REPOSITORY` are **not** in the host `.env` file — they are injected by the CI deploy job via the step's `env:` block. Everything else (connection string, JWT key, email credentials, etc.) lives only in the host `.env` file.

The `DEPLOY_ENV_FILE` repository variable (Settings → Secrets and variables → Actions → Variables) tells the deploy job where to find the `.env` file on the host.

## Migration bundle

`PinballPVPContextFactory` (`Data/PinballPVPContextFactory.cs`) implements `IDesignTimeDbContextFactory<PinballPVPContext>`. This lets the bundle be built — and run — without the full app startup (JWT keys, email config, etc. are not needed just to apply migrations). The factory reads `ConnectionStrings__DefaultConnection` from environment variables, which the `migrate` service provides.

The bundle is built with `--self-contained --runtime linux-x64` so it runs in the `aspnet:10.0` runtime image without needing the SDK.

## Deploy flow

The CI `deploy` job:
1. Checks out the repo (to get the latest `docker-compose.yml`).
2. Computes the short SHA to match the image tag pushed by the `docker` job (`sha-<7 chars>`).
3. Logs in to GHCR.
4. `docker compose ... pull api migrate` — pulls the new images.
5. `docker compose ... up -d --wait` — compose honours the dependency chain: starts `db` (waits for healthy), runs `migrate` (waits for exit 0), starts/recreates `api` (waits for `/health` to return 200).

If migrations fail, `up --wait` exits non-zero and the deploy step fails, leaving the old `api` running.

## Runner setup prerequisites

1. Register a self-hosted runner (Settings → Actions → Runners → New self-hosted runner).
2. Ensure Docker Desktop is running and the runner user can call `docker compose`.
3. Create the secrets file from `.env.example` and set `DEPLOY_ENV_FILE` as a repository variable pointing to it.

## TLS / networking

The API is HTTP-only on port 8080 (host-published). TLS termination, reverse proxy, and public
exposure are still TODO — see [TODO.md](../../TODO.md).
