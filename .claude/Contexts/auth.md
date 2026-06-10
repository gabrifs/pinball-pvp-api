# Authentication, authorization & rate limiting

## Authentication

Auth is JWT-bearer based, configured in `Program.cs` (`AddAuthentication().AddJwtBearer(...)`) using settings
from the `Jwt` config section (`Key`, `Issuer`, `Audience`, `ExpirationMinutes`). Worth knowing:

- `options.MapInboundClaims = false` is set deliberately so claim types stay as issued (`sub`, not the legacy
  `ClaimTypes.NameIdentifier` XML URI). Always read the user id via the `User.GetUserId()` extension
  (`Extensions/ClaimsPrincipalExtensions.cs`), which looks up `JwtRegisteredClaimNames.Sub` — don't add new
  lookups against `ClaimTypes.NameIdentifier`, they won't match.
- `AuthController.Login` validates credentials via `IPasswordHasher` (`Services/Password Hashing/`,
  Argon2-backed) and issues tokens via `IJwtTokenService` (`Services/Auth/`). Both are registered as scoped
  services in `Program.cs` and injected through primary constructors — follow that pattern for new services.
- `IPasswordHasher.Verify(hash, password)` takes the **stored hash first, then the plaintext password** —
  matching the underlying `Argon2.Verify(encoded, password)` signature. Getting this order backwards silently
  breaks all logins (hashes don't parse as passwords or vice versa), so don't "fix" it back to `(password, hash)`.

## Refresh tokens

Refresh tokens are long-lived opaque tokens (30-day default, configurable via `Jwt:RefreshTokenExpirationDays`)
that let clients obtain new access tokens without re-entering credentials. Key design points:

- **Storage:** only the SHA-256 hash of the raw token is persisted (`RefreshToken.TokenHash`); the raw token
  is returned to the client once and never stored server-side — if the DB is leaked, tokens can't be replayed.
- **Rotation:** `POST /api/auth/refresh` revokes the submitted token and issues a fresh pair
  (`token` + `refreshToken`) atomically via an explicit EF Core transaction. The client must update its
  stored refresh token on every refresh.
- **Revocation / logout:** `POST /api/auth/logout` (`[Authorize]`) accepts the client's refresh token,
  validates it belongs to the authenticated user, and revokes it. It is idempotent — submitting an already-
  revoked/expired token returns `204` rather than an error.
- **Single-session policy:** `Login` calls `RevokeAllForUserAsync` before issuing the new token (wrapped in
  an explicit transaction), so only one active refresh token exists per user at any time. This naturally
  cleans up dangling tokens from crashed/disconnected sessions — the player just logs in again.
- **`IRefreshTokenService`** (`Services/Auth/`) handles generation, validation, revocation, and bulk
  revocation; it's a scoped service injected into `AuthController`. The service takes the raw token from
  the client, hashes it, and looks it up — callers never deal with the hash directly.
- **Cascade delete:** deleting a `User` cascades to their `RefreshToken` rows (configured in
  `PinballPVPContext.OnModelCreating`).

## Email service

`IEmailService` (`Services/Email/`) is a scoped service with a single method:
`SendPasswordRecoveryAsync(toEmail, toNickname, recoveryCode)`. The concrete implementation
`SmtpEmailService` uses MailKit's async `SmtpClient` with STARTTLS on the configured port (default 587).

Configuration lives in the `Email` config section. Non-sensitive keys (`Host`, `Port`, `FromAddress`,
`FromName`) belong in `appsettings*.json`; the credentials (`Username`, `Password`) **must** be set
via `dotnet user-secrets` (local dev) or environment variables / a secrets manager (other
environments) — never committed to a config file.

## Password recovery

`POST /api/auth/forgot-password` and `POST /api/auth/reset-password` implement a recovery-code flow,
both rate-limited via `RateLimiterPolicyNames.AuthEndpoints` like `Login`/`Refresh`.

- **`PasswordRecoveryCode`** (`Models/`) — `UserId` (FK to `User`, `DeleteBehavior.Cascade`), `CodeHash`
  (SHA-256 hash of the raw code; the raw code itself is never persisted), `ExpiresAt`, `Used`. Indexed on
  `(UserId, Used)`.
- **`ForgotPassword`** — looks up the user by `dto.UserId`. If not found, returns `Ok()` immediately
  without revealing whether the account exists. Otherwise:
  1. Invalidates any still-active codes for that user via `ExecuteUpdateAsync` (sets `Used = true`), so
     only the most recently issued code is ever valid.
  2. Generates an 8-character uppercase hex code (`RandomNumberGenerator.GetBytes(4)`), hashes it with
     SHA-256 (`AuthController.HashCode`, mirroring `RefreshTokenService`'s hashing pattern), and stores it
     with `ExpiresAt = UtcNow + PasswordRecovery:ExpirationMinutes` (config key, default 15).
  3. Sends the **raw** code and the expiration window to the user's email via
     `IEmailService.SendPasswordRecoveryAsync` (see "Email service" above).
- **`ResetPassword`** — hashes `dto.RecoveryCode` and looks for a matching, unused, unexpired
  `PasswordRecoveryCode` for `dto.UserId`. If found, marks it `Used = true`, overwrites
  `user.PasswordHash` via `IPasswordHasher.Hash`, and revokes all of the user's refresh tokens via
  `IRefreshTokenService.RevokeAllForUserAsync` — mirroring the single-session policy enforced on `Login`,
  since a password reset is often prompted by a compromised credential.
- **Config:** `PasswordRecovery:ExpirationMinutes` (default 15 if absent) controls code lifetime and is
  passed through to `SendPasswordRecoveryAsync` so the email text always matches the configured window.
- **Cleanup:** `ExpiredRecordPurgeService` bulk-deletes `PasswordRecoveryCode` rows that are `Used` or
  past `ExpiresAt`, alongside expired `RefreshToken`/`PendingVersusMatch` rows (see
  [persistence.md](persistence.md)).

## Rate limiting

`POST /api/auth` (login) and `POST /api/users` (registration) are unauthenticated and thus the prime targets
for brute-force/spam abuse. Both carry `[EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]`
(`Microsoft.AspNetCore.RateLimiting`), backed by a single named policy registered in `Program.cs` via
`AddRateLimiter` — a sliding-window limiter (5 requests/minute, partitioned by client IP) that returns
`429 Too Many Requests` with a `Retry-After` header on rejection. `RateLimiterPolicyNames`
(`Services/Rate Limiting/`) holds the policy name as a constant shared between `Program.cs` and the
controllers — add new constants there rather than repeating policy-name string literals. Note both
endpoints share the **same** per-IP bucket: this is deliberate (one combined "auth abuse" budget per client,
so switching endpoints can't be used to dodge throttling), not an oversight — don't split them into separate
policies without a reason to.

## Authorization pattern for match creation

`[Authorize]` alone isn't sufficient — it only proves *someone* is logged in, not that they're the player the
match is being recorded for. `SoloMatchesController.CreateMatch` and `VersusMatchesController.CreateMatch`
both additionally check that `User.GetUserId()` matches a player id named in the request DTO (`dto.UserId`
for solo; `dto.WinnerId` or `dto.LoserId` for versus, since either participant may act as the P2P host
reporting the result), returning `Forbid()` otherwise. Apply the same "caller must be a named participant"
check to any new endpoint that lets a player submit data on their own behalf. Note this still trusts a
participant's report of *who won* a versus match — see [TODO.md](../../TODO.md).
