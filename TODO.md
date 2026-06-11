# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Deployment

- [ ] **Implement self-hosted docker-compose deployment** — architecture is decided: a
  self-hosted GitHub Actions runner on a Windows PC (Docker Desktop) runs the CI deploy step
  directly; `docker-compose.yml` defines `db` (Postgres, persistent volume, not exposed to the
  host) + `api` (the only published port, HTTP-only on 8080) + a one-off `migrate` service that
  applies pending migrations via an EF Core migration bundle before `api` is rolled out. Secrets
  live in a `.env` file on the host, outside the git checkout. See the comment at the bottom of
  [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

- [ ] **TLS / reverse proxy / public network exposure** — once the deployment above is live, the
  API runs HTTP-only on port 8080, reachable over LAN/port-forward only. Decide on a TLS
  termination strategy (e.g. a reverse proxy such as Caddy/nginx/Traefik added to the compose
  stack, or a tunnel like Cloudflare Tunnel/Tailscale Funnel) before exposing the API beyond the
  local network. Once decided, revisit `UseHttpsRedirection()` (currently a no-op — see
  [persistence.md](.claude/Contexts/persistence.md)) and the `ASPNETCORE_HTTP_PORTS`/`EXPOSE`
  settings in the Dockerfile if an HTTPS port needs to be bound directly.

- [ ] **Database backups** — once Postgres is self-hosted as a docker-compose container, it
  becomes the permanent home for production player data with no managed backups; a host disk
  failure would mean total data loss. Plan and implement a backup strategy (e.g. scheduled
  `pg_dump` of the `pgdata` volume to an external/offsite location).

## Performance

- [ ] **Limit leaderboard queries to the top 100** — `LeaderboardService`'s aggregation
  helpers (`GetSoloStatsAsync`/`GetVersusStatsAsync`, see
  [controllers.md](.claude/Contexts/controllers.md)) currently load every player's stats for the
  selected period before sorting and paginating in memory. As the player base grows this becomes
  an unbounded query/aggregation over the whole table. Investigate capping the overall
  leaderboards to the top 100 players (e.g. push sorting/limiting into the SQL query) instead of
  loading and sorting the full result set.

## Reliability

- [ ] **Make match-creation win/loss updates idempotent** — `SoloMatchService` and
  `VersusMatchService` increment `PlayerRecord`/`AllTimeBestRecord` win/loss counters
  directly as a side effect of `CreateMatchAsync` (see [entities.md](.claude/Contexts/entities.md)).
  A retried request (client timeout, dropped connection, reconnect resubmitting the same result)
  could double-count a win or loss. Review for idempotency — e.g. a client-supplied idempotency
  key, or detecting/deduplicating near-identical recent submissions.
