# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Deployment

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
