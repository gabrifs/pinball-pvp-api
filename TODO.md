# TODO — Path to Production

A roadmap of what's still needed to take PinballPVP.Api from its current development state to a
production-ready backend for the Unity head-to-head pinball game. Items are grouped by area, not by priority.

## Database Maintenance

- [ ] **Yearly data rollover** — At year end, capture the year's top 3, reset year-to-date stats, and prune old match data to keep the database size bounded.
  - Add `AllTimeBestRecord` table: one row per player, maintained continuously as a side effect of match creation. Tracks six metrics, each with a companion year field: `SoloHighscore` / `SoloHighscoreYear`, `SoloWins` / `SoloWinsYear`, `SoloMatchesPlayed` / `SoloMatchesPlayedYear`, `VersusHighscore` / `VersusHighscoreYear`, `VersusWins` / `VersusWinsYear`, `VersusMatchesPlayed` / `VersusMatchesPlayedYear`. `UserId` is the PK and FK (`DeleteBehavior.Restrict`). All value fields initialised to zero, year fields to null, at user registration alongside `PlayerRecord`. Updated in the same transaction as `PlayerRecord` on match creation: if the updated `PlayerRecord` value exceeds the stored best, overwrite both the value and its year field. `MatchesPlayed` is derived as `SoloWins + SoloLosses` (or versus equivalent) from `PlayerRecord` at update time. Win rate and losses are intentionally excluded — they don't have clear "bigger is always better" semantics.
  - Add `YearlyLeaderboardEntry` table: top 3 per category per year. Fields: `Year`, `Category` (enum: `SoloHighscore`, `SoloWins`, `SoloWinRate`, `VersusHighscore`, `VersusWins`, `VersusWinRate`), `Rank` (1–3), `UserId` (FK nullable, `DeleteBehavior.SetNull`), `NicknameSnapshot` (frozen at capture time), `Value` (double — winrates stored as percentages, integer counts cast to double for uniformity). Unique index on `(Year, Category, Rank)`.
  - Add `GetYearRange(int year)` public static method to `PeriodFilterExtensions` — returns `(DateTime Start, DateTime End)` for the given year using the same UTC-safe pattern already used by `GetPeriodRange`. Refactor the `"year"` case in `GetPeriodRange` to delegate to it. The purge service uses this for per-year scoping; `IsValidPeriod` / `ApplyPeriodFilter` are unaffected.
  - Extend `ExpiredRecordPurgeService` to detect year rollover (matches exist where `PlayedAt < Jan 1 of the current year`). For each distinct prior year, process in chronological order:
    1. Compute top 3 per category from current `PlayerRecord` rows (apply `Leaderboard:WinRateMinMatches` threshold for WinRate categories) → `YearlyLeaderboardEntry`
    2. Reset all `PlayerRecord` aggregate fields to zero — after this point `PlayerRecord` is year-to-date only
    3. Bulk-delete that year's matches via `ExecuteDeleteAsync` scoped with `GetYearRange`
  - Add EF Core migrations for both new tables.
  - Add endpoint `GET /api/v1/leaderboards/yearly/{year}` returning `YearlyLeaderboardEntry` data for the given year, grouped by category.
  - Update `Claude/entities.md` to document that `PlayerRecord` is year-to-date (resets each rollover), and `AllTimeBestRecord` is the continuously-maintained personal best per player with the year each record was set.

## Leaderboards

- [ ] **WinRate leaderboard — minimum matches threshold** — A player with one win and zero losses sits at 100% and tops the board. Add `Leaderboard:WinRateMinMatches` (default `10`, configurable for testing) to `appsettings.json`. Filter out players where `Wins + Losses < threshold` in `GetSoloWinRateLeaderboard`, `GetVersusWinRateLeaderboard`, and the WinRate rank computation inside `GET /leaderboards/player/{userId}`. Apply the same threshold when computing `SoloWinRate` and `VersusWinRate` entries in `YearlyLeaderboardEntry` during rollover. Win/Loss counts are already present in the leaderboard DTOs — no DTO changes needed.

## Deployment

- [ ] **Configure deploy step in CI/CD** — pipeline builds, tests, and pushes the image; the deploy
  step (run migrations + roll out new container) is a placeholder until a hosting target is chosen.
  See the comment at the bottom of [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
