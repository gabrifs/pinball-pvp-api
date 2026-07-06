# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Deployment

- [ ] **Database backups** — once Postgres is self-hosted as a docker-compose container, it
  becomes the permanent home for production player data with no managed backups; a host disk
  failure would mean total data loss. Plan and implement a backup strategy (e.g. scheduled
  `pg_dump` of the `pgdata` volume to an external/offsite location).
