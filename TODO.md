# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Features

- [ ] **Password recovery — `PasswordRecoveryCode` entity + migration.** Stores `UserId`, a hashed
      recovery code, an expiry timestamp, and a `Used` flag. Index on `(UserId, Used)`.

- [ ] **Password recovery — email service.** Introduce `IEmailService` and a concrete implementation
      (SMTP or a provider such as SendGrid). Wire up configuration (host/port/credentials or API key)
      via user-secrets / environment variables — never committed to `appsettings*.json`.

- [ ] **Password recovery — `POST /api/v1/auth/forgot-password`.** Accepts `{ userId }`. Looks up the
      user's email from the database, generates a cryptographically random recovery code, stores it
      hashed with an expiry (e.g. 15 minutes), and sends the plaintext code to the user's email.
      Always returns `200 OK` regardless of whether the user exists (prevents user enumeration).

- [ ] **Password recovery — `POST /api/v1/auth/reset-password`.** Accepts
      `{ userId, recoveryCode, newPassword }`. Validates the code against the stored hash, checks it
      hasn't expired or been used, hashes the new password, saves it, and marks the recovery code as
      used — all atomically. Returns `400` on invalid/expired code.

## Testing

- [ ] **Add a test project.** There are currently no automated tests in the solution — at minimum, cover the
      controller logic (auth checks, uniqueness validation, win/loss + highscore aggregation) and the
      period-filter helpers with unit tests, plus integration tests against a real (test) database.

## Deployment

- [ ] **Containerize the app** (Dockerfile) for consistent builds and deployment.
- [ ] **Set up CI/CD** (build, test, run migrations, deploy) — no pipeline exists yet.
- [ ] **Document production configuration** (connection strings, JWT settings, allowed hosts/CORS origins) as
      environment variables, separate from the checked-in `appsettings.Development.json`.
