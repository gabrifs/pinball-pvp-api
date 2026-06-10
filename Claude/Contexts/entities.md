# Entities and relationships

`Models/`, configured in `Data/PinballPVPContext.OnModelCreating`.

- `User` — has unique indexes on `Username`, `Nickname`, and `Email`; stores `PasswordHash` (Argon2-hashed,
  never the raw password).
- `User` 1:1 `PlayerRecord` — `PlayerRecord.UserId` is both its PK and its FK to `User`. Every new `User`
  is created together with an empty `PlayerRecord` (see `UsersController.CreateUser`).
- `User` 1:N `SoloMatch` — FK `SoloMatch.UserId`, `DeleteBehavior.Restrict`.
- `User` 1:N `VersusMatch` twice over — `VersusMatch.WinnerId` and `VersusMatch.LoserId` both point to
  `User`, each with its own navigation property (`Winner` / `Loser`) and `DeleteBehavior.Restrict`.
- `PlayerRecord` aggregates win/loss counts and highscores separately for solo and versus play
  (`SoloWins`, `SoloLosses`, `SoloHighscore`, `VersusWins`, `VersusLosses`, `VersusHighscore`). These are
  updated as a side effect when a match is created (see [controllers.md](controllers.md)) — they are not
  recomputed from match history.
- `PendingVersusMatch` — temporary holding table for the dual-confirmation anti-spoofing flow (see
  [controllers.md](controllers.md)). Has `ReporterId`, `WinnerId`, `LoserId`, the six score fields, and
  `ExpiresAt` (5 minutes from creation, controlled by `PendingVersusMatch.ConfirmationWindowMinutes`).
  `MinPlayerId`/`MaxPlayerId` are `Math.Min/Max(WinnerId, LoserId)` set on insert — they back a unique index
  ensuring at most one pending match per player pair regardless of who claims to have won. All three FKs
  (`ReporterId`, `WinnerId`, `LoserId`) use `DeleteBehavior.Restrict` — consistent with the `VersusMatch`
  FKs.
