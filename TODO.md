# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Deployment

- [ ] **Evaluate Fly.io as hosting target** — assess whether Fly.io is a good fit for containerized deployment of this API (pricing, Postgres managed DB, secrets management, region selection, cold-start behaviour for a game backend). Decision gates the deploy step below.
- [ ] **Configure deploy step in CI/CD** — pipeline builds, tests, and pushes the image; the deploy
  step (run migrations + roll out new container) is a placeholder until a hosting target is chosen.
  See the comment at the bottom of [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
