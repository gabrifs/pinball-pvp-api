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
