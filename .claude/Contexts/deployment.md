# Deployment

## Architecture

GitHub-hosted (`ubuntu-latest`) runner deploys to a Windows PC running Docker Desktop. On every
push to `master`:

1. **build-and-test** (ubuntu-latest) — runs the full test suite.
2. **docker** (ubuntu-latest) — builds and pushes the image to GHCR as `:latest` and `:sha-<short-sha>`.
3. **deploy** (ubuntu-latest, `environment: production`) — joins the project's Tailscale network,
   SSHes into the host over that private network via Docker's SSH context backend, pulls the new
   image, runs migrations, rolls out the API.

The host's ISP (Vivo, residential fiber) puts it behind CGNAT — there is no public IP to forward a
router port to, so the deploy connection and the public-facing API both have to be reachable
without any inbound port-forward. Tailscale is the mechanism for both: the CI runner reaches the
host over Tailscale's outbound-only tunnel for deploys, and `tailscale funnel` separately exposes
the `api` service to the public internet — see [Router / public exposure](#router--public-exposure).

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

## Deploy mechanism: Tailscale + Docker SSH context

The `Connect to Tailscale` step (`tailscale/github-action@v4`) joins the runner to the project's
tailnet as an ephemeral, tagged (`tag:ci`) node before anything else runs. Once connected, the
runner can reach the host's Tailscale IP/MagicDNS name as if it were on the same private network —
no public inbound port on the host is involved at any point.

The deploy job then creates a Docker context that proxies all Docker API calls to the remote daemon
through an SSH tunnel, addressed via that Tailscale hostname:

```bash
docker context create production \
  --docker "host=ssh://<user>@<tailscale-host>:22"
```

Then all compose commands run as `docker --context production compose ...`. The runner's `docker`
CLI speaks to the remote Docker Desktop daemon without any docker daemon on the runner itself — it
does this by shelling out to the system `ssh` client (visible in error output as
`ssh -o ConnectTimeout=... -T -l <user> -p <port> -- <host> docker system dial-stdio>`), not a Go
SSH implementation, so ordinary OpenSSH client config applies.

**Gotcha:** the private key is written to `~/.ssh/deploy_key`, which is not one of OpenSSH's default
identity filenames (`id_rsa`, `id_ed25519`, etc.) — without an explicit `~/.ssh/config` entry, the
client silently never offers it and falls back to password auth (which then fails, since there's no
interactive terminal). The "Set up SSH" step in `ci.yml` therefore also writes:

```ssh-config
Host <DEPLOY_HOST>
  User <DEPLOY_SSH_USER>
  Port <DEPLOY_SSH_PORT>
  IdentityFile ~/.ssh/deploy_key
  IdentitiesOnly yes
```

If this step is ever refactored, keep this config block — its absence produces a generic
"Permission denied (publickey,password,keyboard-interactive)" error that looks identical to a host-side
misconfiguration (wrong key, wrong permissions, wrong group), which is a much harder problem to debug.

All `${VAR}` substitutions in `docker-compose.yml` are satisfied by the step `env:` blocks on the
runner — Docker Compose reads from the process environment. No `.env` file is written anywhere.

## Secrets and variables

All values are stored in GitHub Actions (Settings → Secrets and variables → Actions), scoped to the
`production` environment where possible.

**Secrets** (sensitive):

- `DEPLOY_SSH_KEY` — Ed25519 private key for SSH access to the host
- `TS_OAUTH_CLIENT_ID`, `TS_OAUTH_SECRET` — Tailscale OAuth client credentials used to authenticate
  the ephemeral CI node (created in the Tailscale admin console; must be scoped to a tag, e.g.
  `tag:ci`, since an OAuth client isn't associated with a user)
- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- `CONNECTION_STRING` — full Npgsql connection string (`Host=db;Database=...;Username=...;Password=...`)
- `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRATION_MINUTES`, `JWT_REFRESH_TOKEN_EXPIRATION_DAYS`
- `EMAIL_HOST`, `EMAIL_PORT`, `EMAIL_FROM_ADDRESS`, `EMAIL_FROM_NAME`, `EMAIL_USERNAME`, `EMAIL_PASSWORD`
- `CORS_ALLOWED_ORIGIN_0` — production frontend URL (the only origin the API will accept cross-origin requests from)

**Variables** (non-sensitive):

- `DEPLOY_HOST` — the host's Tailscale IP (`100.x.y.z`) or MagicDNS name (not a public IP/domain —
  there isn't one, see [Router / public exposure](#router--public-exposure))
- `DEPLOY_SSH_PORT` — `22` (the real SSH port; no longer a router-exposed custom port, since traffic
  never touches the public internet)
- `DEPLOY_SSH_USER` — Windows user account name on the host
- `LEADERBOARD_WIN_RATE_MIN_MATCHES`, `MAINTENANCE_PURGE_INTERVAL_HOURS`, `PASSWORD_RECOVERY_EXPIRATION_MINUTES`

`IMAGE_TAG` and `GITHUB_REPOSITORY` are computed by the deploy job itself (not stored as secrets/vars).

## Migration bundle

`PinballPVPContextFactory` (`Data/PinballPVPContextFactory.cs`) implements `IDesignTimeDbContextFactory<PinballPVPContext>`. This lets the bundle be built — and run — without the full app startup (JWT keys, email config, etc. are not needed just to apply migrations). The factory reads `ConnectionStrings__DefaultConnection` from environment variables, which the `migrate` service provides.

The bundle is built with `--self-contained --runtime linux-x64` so it runs in the `aspnet:10.0` runtime image without needing the SDK.

## Deploy flow

The CI `deploy` job:

1. Checks out the repo (to get the latest `docker-compose.yml` for the runner's compose client).
2. Joins the tailnet as an ephemeral `tag:ci` node (`tailscale/github-action@v4`).
3. Computes the short SHA to match the image tag pushed by the `docker` job (`sha-<7 chars>`).
4. Writes the SSH private key and sets up `known_hosts` via `ssh-keyscan` against the host's
   Tailscale address.
5. Creates the `production` Docker context with the SSH backend (addressed over Tailscale).
6. Logs in to GHCR.
7. `docker --context production compose pull api migrate` — pulls the new images on the remote host.
8. `docker --context production compose up -d --wait` — compose honours the dependency chain:
   starts `db` (waits for healthy), runs `migrate` (waits for exit 0), starts/recreates `api`
   (waits for `/health` to return 200).

If migrations fail, `up --wait` exits non-zero and the deploy step fails, leaving the old `api` running.

## Host prerequisites (one-time setup)

1. Install Tailscale on the host and log it into this project's tailnet (`tailscale up`).

2. Enable Windows OpenSSH Server (Settings → Optional Features → OpenSSH Server).

3. Generate a deploy key pair: `ssh-keygen -t ed25519 -f deploy_key -C "github-deploy"`

4. Add the public key to `C:\Users\<user>\.ssh\authorized_keys` on the host.

5. Store the private key content as `DEPLOY_SSH_KEY` GitHub secret.

6. Create a Tailscale OAuth client (admin console → Settings → OAuth clients) scoped to a `tag:ci`
   tag with the `auth_keys` write scope; store its ID/secret as `TS_OAUTH_CLIENT_ID`/`TS_OAUTH_SECRET`
   GitHub secrets. Add `tag:ci` to the tailnet's ACL policy file if it isn't already defined.

7. Docker Desktop must be running on the host; `docker` must be in PATH for the SSH user (Docker
   Desktop ensures this).

8. No runner agent to install or keep running — only OpenSSH Server and Tailscale need to be active.

9. Restrict the Windows Firewall's inbound SSH rule to the Tailscale interface (`100.64.0.0/10`)
   rather than "Any", since SSH is no longer reachable from the public internet at all.

## Router / public exposure

The host is behind CGNAT on its residential ISP (Vivo) — there is no public IP to forward a router
port to, and no router setting can change that (only the ISP can, by disabling CGNAT on the line,
which was requested but unavailable/unconfirmed at the time this was written). **No router port
forwarding and no DDNS are used or needed** — both were part of an earlier plan superseded by
Tailscale once CGNAT was confirmed.

Instead:

- **Deploy access** — handled entirely by the `Connect to Tailscale` CI step; see
  [Deploy mechanism](#deploy-mechanism-tailscale--docker-ssh-context).
- **Public API access** — `tailscale funnel --bg 8080` run once on the host exposes the `api`
  service (already published to the host's `localhost:8080` by `docker-compose.yml`) at
  `https://<host>.<tailnet>.ts.net`, over HTTPS, with Tailscale handling TLS termination and
  certificate renewal automatically. This resolves the TLS/plaintext-exposure item that was
  previously tracked in [TODO.md](../../TODO.md) as an accepted temporary risk — Funnel provides
  real TLS with no reverse proxy to maintain.
- Point the Unity client's API base URL at that `ts.net` hostname (stable — doesn't change with the
  host's underlying IP, so no DDNS equivalent is needed here either).
- `tailscale funnel --bg` persists across reboots and Tailscale restarts; re-run it manually only if
  the host is fully re-provisioned or removed/re-added to the tailnet.

## Database backups

[`scripts/backup-database.ps1`](../../scripts/backup-database.ps1) runs on the host on a schedule
(Windows Task Scheduler — not CI, since this is an ongoing host-side operational concern rather than
something tied to a deploy). It covers both halves of the 3-2-1 rule this project needs:

- **Local** dumps in a local directory (default `C:\pinball-pvp\backups`, overridable — point it at a
  different physical disk than the OS/Docker if you want the local copy to survive a single-disk
  failure too), pruned to a retention window (default 14 days).
- **Offsite**, via `rclone sync` to Google Drive — `sync` (not `copy`) mirrors deletions too, so the
  remote copy tracks the same retention window without separate cleanup logic.

The script finds the running `db` container by its `com.docker.compose.service=db` label rather than
a hardcoded name (the project name prefix depends on the checkout directory the `deploy` job used).
It needs no database credentials: `pg_dump` runs *inside* the container via `docker exec`, using the
`POSTGRES_USER`/`POSTGRES_DB` environment variables `docker-compose.yml` already injects into it,
authenticating over the local Unix socket — trusted by default in the official `postgres` image
regardless of the `host`-line auth method. The dump is written inside the container and pulled out
with `docker cp` rather than piped through PowerShell's stdout pipeline, since PowerShell can
silently alter line endings/add a BOM when capturing external process output as text.

The whole script body runs inside a `try/catch`: on any failure it emails `BACKUP_ALERT_TO_ADDRESS`
via Gmail SMTP, then re-throws so Task Scheduler still records a non-zero result even if the email
itself can't be sent. All three alert-related values
(`BACKUP_ALERT_SMTP_USERNAME`/`BACKUP_ALERT_SMTP_PASSWORD`/`BACKUP_ALERT_TO_ADDRESS`) are read from
environment variables set at the machine level on the host — **not** GitHub Actions secrets, since
this script runs entirely via Task Scheduler outside of any workflow run, so GitHub's secret store
is simply unreachable from it. If any of the three are unset, a warning is printed and the script
still fails loudly via its exit code, just without an email.

**Non-ASCII characters (em dashes, curly quotes, etc.) must not be used in this script.** Windows
PowerShell 5.1 reads `.ps1` files without a byte-order-mark using the system's ANSI codepage, not
UTF-8 — on a non-English Windows install (this host is Portuguese-locale), an em dash silently
corrupts into different bytes and breaks parsing with confusing, seemingly-unrelated errors several
lines away from the actual character. This bit us once already during setup; keep the script pure
ASCII rather than relying on encoding/BOM correctness across git/editors/locales.

**One-time host setup:**

1. Install [rclone](https://rclone.org) and run `rclone config` to add a remote named `gdrive`
   (interactive OAuth flow, opens a browser).

2. Set the failure-alert environment variables at the machine level (SMTP credentials can reuse the
   same Gmail account/app password already set up for the API's own `EMAIL_*` secrets, or a different
   account — the two are unrelated):

   ```powershell
   [Environment]::SetEnvironmentVariable("BACKUP_ALERT_SMTP_USERNAME", "<gmail-address>", "Machine")
   [Environment]::SetEnvironmentVariable("BACKUP_ALERT_SMTP_PASSWORD", "<gmail-app-password>", "Machine")
   [Environment]::SetEnvironmentVariable("BACKUP_ALERT_TO_ADDRESS", "<address-to-notify>", "Machine")
   ```

3. Register the script in Task Scheduler to run daily. Deliberately no inner quotes around the
   script path in `-Argument` below — it contains no spaces, and nesting quotes inside a native/task
   command from PowerShell is fragile (this also bit us once already; see the `-N ""` and SSH
   identity file issues from earlier in this project's setup, and `New-ScheduledTaskAction` silently
   misparsed a quoted path into `WorkingDirectory` instead of `Arguments` the first time this was tried):

   ```powershell
   $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File C:\path\to\scripts\backup-database.ps1"
   $trigger = New-ScheduledTaskTrigger -Daily -At 3am
   $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
   Register-ScheduledTask -TaskName "PinballPvP-DB-Backup" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest
   ```

   `-StartWhenAvailable` catches up on a missed run if the host happens to be off/rebooting at the
   scheduled time (e.g. during Windows Update), rather than silently skipping that day's backup.
   `-ExecutionTimeLimit` kills the task if `docker`/`rclone` ever hangs, instead of leaving it running
   indefinitely.

   Verify the registration actually took the path correctly before relying on it:

   ```powershell
   (Get-ScheduledTask -TaskName "PinballPvP-DB-Backup").Actions
   ```

   `Arguments` should show the full `-ExecutionPolicy Bypass -File C:\...\backup-database.ps1` as one
   piece, and `WorkingDirectory` should be empty.

**Restore procedure** (verified working during implementation — plain SQL dump, restored via `psql`,
not `pg_restore`, since the dump isn't in custom/compressed format):

```powershell
docker cp <path-to-backup.sql> <db-container-name>:/tmp/restore.sql
docker exec <db-container-name> sh -c "psql -U `$POSTGRES_USER -d `$POSTGRES_DB -f /tmp/restore.sql"
```

Restoring into a database that already has the schema/data will produce constraint-violation errors
on the `COPY`/`INSERT` statements for anything that already exists — this restores into an *empty*
database (e.g. a fresh `db` container after a disaster), not as a merge into a live one.
