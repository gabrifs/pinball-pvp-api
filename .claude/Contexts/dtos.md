# DTOs (`Dtos/`)

DTOs are grouped into feature subfolders (`User/`, `Matches/`, `Player Records/`, `Login/`, `Leaderboards/`)
but all share the flat `PinballPVP.Api.Dtos` namespace — folder structure is for organization only.

Every response DTO (`UserResponseDto`, `SoloMatchResponseDto`, `VersusMatchResponseDto`,
`PlayerRecordResponseDto`, ...) follows the same shape and should be extended the same way when adding new ones:

- A static `Expression<Func<TEntity, TDto>> Projection` used inside EF `.Select(...)` so the SQL query only
  fetches the needed columns.
- A static `TDto FromEntity(TEntity entity)` factory used for in-memory mapping after an insert (e.g. inside
  `CreatedAtAction` responses), where a LINQ expression can't be evaluated.

Both members must be kept in sync — they map the same fields, just for different evaluation contexts
(SQL translation vs. in-memory).

Request DTOs (`CreateUserDto`, `CreateSoloMatchDto`, `CreateVersusMatchDto`, `LoginDto`) are plain records
validated via `DataAnnotations` attributes (`[Required]`, `[StringLength]`); validation is wired up via
`builder.Services.AddValidation()` in `Program.cs`.
