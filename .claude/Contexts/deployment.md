# Deployment

## Architecture

GitHub-hosted (`ubuntu-latest`) runner deploys to a Windows PC running Docker Desktop. On every
push to `master`:

1. **build-and-test** (ubuntu-latest) — runs the full test suite.
2. **docker** (ubuntu-latest) — builds and pushes the image to GHCR as `:latest` and `:sha-<short-sha>`.
3. **deploy** (ubuntu-latest, `environment: production`) — SSHes into the host via Docker's SSH
   context backend, pulls the new image, runs migrations, rolls out the API.

## Files

- **`Dockerfile`** — multi-stage build: publishes the API and builds an EF Core migration bundle
  (`efbundle`) in the same stage. Both end up in the runtime image at `/app/`. The `migrate` service
  in docker-compose overrides the entrypoint to `["./efbundle"]`.
- **`docker-compose.yml`** — three services:
  - `db` — Postgres 17 with a named volume (`pgdata`) and a `pg_isready` healthcheck; not exposed to the host.
  - `migrate` — one-off service (`restart: "no"`) that runs `efbundle`, applying pending migrations. Depends on `db` being healthy.
  - `api` — the app, published on port 8080. Depends on `migrate` completing successfully (`service_completed_successfully`).
- **`.env.example`** — reference template for `docker-compose.yml` variable substitution. Not used
  by CI — useful only for manual `docker compose` runs on the host.
- **`.gitignore`** — `.env` is ignored; only `.env.example` is tracked.

## Deploy mechanism: Docker SSH context

The deploy job creates a Docker context that proxies all Docker API calls to the remote daemon
through an SSH tunnel:

```bash
docker context create production \
  --docker "host=ssh://<user>@<host>:<port>"
```

Then all compose commands run as `docker --context production compose ...`. The runner's `docker`
CLI speaks to the remote Docker Desktop daemon without any docker daemon on the runner itself.

All `${VAR}` substitutions in `docker-compose.yml` are satisfied by the step `env:` blocks on the
runner — Docker Compose reads from the process environment. No `.env` file is written anywhere.

## Secrets and variables

All values are stored in GitHub Actions (Settings → Secrets and variables → Actions), scoped to the
`production` environment where possible.

**Secrets** (sensitive):

- `DEPLOY_SSH_KEY` — Ed25519 private key for SSH access to the host
- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- `CONNECTION_STRING` — full Npgsql connection string (`Host=db;Database=...;Username=...;Password=...`)
- `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRATION_MINUTES`, `JWT_REFRESH_TOKEN_EXPIRATION_DAYS`
- `EMAIL_HOST`, `EMAIL_PORT`, `EMAIL_FROM_ADDRESS`, `EMAIL_FROM_NAME`, `EMAIL_USERNAME`, `EMAIL_PASSWORD`
- `CORS_ALLOWED_ORIGIN_0` — production frontend URL (the only origin the API will accept cross-origin requests from)

**Variables** (non-sensitive):

- `DEPLOY_HOST` — public IP or domain of the host
- `DEPLOY_SSH_PORT` — SSH port (22 or custom)
- `DEPLOY_SSH_USER` — Windows user account name on the host
- `LEADERBOARD_WIN_RATE_MIN_MATCHES`, `MAINTENANCE_PURGE_INTERVAL_HOURS`, `PASSWORD_RECOVERY_EXPIRATION_MINUTES`

`IMAGE_TAG` and `GITHUB_REPOSITORY` are computed by the deploy job itself (not stored as secrets/vars).

## Migration bundle

`PinballPVPContextFactory` (`Data/PinballPVPContextFactory.cs`) implements `IDesignTimeDbContextFactory<PinballPVPContext>`. This lets the bundle be built — and run — without the full app startup (JWT keys, email config, etc. are not needed just to apply migrations). The factory reads `ConnectionStrings__DefaultConnection` from environment variables, which the `migrate` service provides.

The bundle is built with `--self-contained --runtime linux-x64` so it runs in the `aspnet:10.0` runtime image without needing the SDK.

## Deploy flow

The CI `deploy` job:
1. Checks out the repo (to get the latest `docker-compose.yml` for the runner's compose client).
2. Computes the short SHA to match the image tag pushed by the `docker` job (`sha-<7 chars>`).
3. Writes the SSH private key and sets up `known_hosts` via `ssh-keyscan`.
4. Creates the `production` Docker context with the SSH backend.
5. Logs in to GHCR.
6. `docker --context production compose pull api migrate` — pulls the new images on the remote host.
7. `docker --context production compose up -d --wait` — compose honours the dependency chain:
   starts `db` (waits for healthy), runs `migrate` (waits for exit 0), starts/recreates `api`
   (waits for `/health` to return 200).

If migrations fail, `up --wait` exits non-zero and the deploy step fails, leaving the old `api` running.

## Host prerequisites (one-time setup)

1. Enable Windows OpenSSH Server (Settings → Optional Features → OpenSSH Server).

2. Generate a deploy key pair: `ssh-keygen -t ed25519 -f deploy_key -C "github-deploy"`

3. Add the public key to `C:\Users\<user>\.ssh\authorized_keys` on the host.

4. Store the private key content as `DEPLOY_SSH_KEY` GitHub secret.

5. Docker Desktop must be running on the host; `docker` must be in PATH for the SSH user (Docker
   Desktop ensures this).

6. No runner agent to install or keep running — only OpenSSH Server needs to be active.

## TLS / networking

The API is HTTP-only on port 8080 (host-published). TLS termination, reverse proxy, and public
exposure are still TODO — see [TODO.md](../../TODO.md).
