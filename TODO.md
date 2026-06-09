# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Features

## Testing

- [ ] **Add a test project.** There are currently no automated tests in the solution — at minimum, cover the
      controller logic (auth checks, uniqueness validation, win/loss + highscore aggregation) and the
      period-filter helpers with unit tests, plus integration tests against a real (test) database.

## Deployment

- [ ] **Containerize the app** (Dockerfile) for consistent builds and deployment.
- [ ] **Set up CI/CD** (build, test, run migrations, deploy) — no pipeline exists yet.
- [ ] **Document production configuration** (connection strings, JWT settings, allowed hosts/CORS origins) as
      environment variables, separate from the checked-in `appsettings.Development.json`.
