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
  recomputed from match history. **Year-to-date only**: the yearly rollover (see
  [persistence.md](persistence.md)) resets all six fields to zero at the start of each year.
- `User` 1:1 `AllTimeBestRecord` — `AllTimeBestRecord.UserId` is both its PK and its FK to `User`
  (`DeleteBehavior.Restrict`). Continuously-maintained personal bests, alongside the year each was set:
  `SoloHighscore`/`SoloHighscoreYear`, `SoloWins`/`SoloWinsYear`, `SoloMatchesPlayed`/`SoloMatchesPlayedYear`,
  and the `Versus` equivalents. `UpdateFromSolo`/`UpdateFromVersus` overwrite a metric (and its year) only
  if the current `PlayerRecord` value exceeds the stored best — called from `SoloMatchesController`/
  `VersusMatchesController` right after the `PlayerRecord` update on every match. Every new `User` is
  created together with an empty `AllTimeBestRecord` (see `UsersController.CreateUser`), like
  `PlayerRecord`.
- `PendingVersusMatch` — temporary holding table for the dual-confirmation anti-spoofing flow (see
  [controllers.md](controllers.md)). Has `ReporterId`, `WinnerId`, `LoserId`, the six score fields, and
  `ExpiresAt` (5 minutes from creation, controlled by `PendingVersusMatch.ConfirmationWindowMinutes`).
  `MinPlayerId`/`MaxPlayerId` are `Math.Min/Max(WinnerId, LoserId)` set on insert — they back a unique index
  ensuring at most one pending match per player pair regardless of who claims to have won. All three FKs
  (`ReporterId`, `WinnerId`, `LoserId`) use `DeleteBehavior.Restrict` — consistent with the `VersusMatch`
  FKs.
- `YearlyLeaderboardEntry` — top-3 snapshot per category per year, captured by the yearly rollover (see
  [persistence.md](persistence.md)). `UserId` is nullable (`DeleteBehavior.SetNull`) so the entry survives
  account deletion; `NicknameSnapshot` freezes the display name at capture time. Unique index on
  `(Year, Category, Rank)`. `Category` is the `YearlyLeaderboardCategory` enum (`SoloHighscore`, `SoloWins`,
  `SoloWinRate`, `VersusHighscore`, `VersusWins`, `VersusWinRate`), stored as its default `int`.
