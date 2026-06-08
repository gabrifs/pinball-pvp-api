# PinballPVP API

A REST API backing a Unity **head-to-head pinball game**, built with **ASP.NET Core (.NET 10)** and
**PostgreSQL**. It handles player accounts and authentication, tracks solo (vs CPU) and versus (P2P
player-vs-player) matches, and maintains an aggregated player record (wins, losses and highscores) for
each player — the foundation for the in-game leaderboards.

> Looking for what's left before this is production-ready? See [TODO.md](TODO.md).

## Features

- **User accounts & authentication** — registration with unique usernames, nicknames and emails,
  Argon2-hashed passwords, and JWT bearer login.
- **Solo matches** — log a single-player session (final score, rounds won, win/loss) against your own highscore.
- **Versus matches** — log head-to-head P2P results between two users (one acts as host and reports the
  result), automatically updating both players' records.
- **Player records** — aggregated solo/versus win-loss counts and highscores per user.
- **Period filters** — list matches filtered by `week`, `month` or `year`.
- **Swagger / OpenAPI** UI for exploring and testing the API in development.

## Tech stack

- [ASP.NET Core 10](https://learn.microsoft.com/aspnet/core) Web API
- [Entity Framework Core](https://learn.microsoft.com/ef/core) with [Npgsql](https://www.npgsql.org/efcore/) (PostgreSQL provider)
- [EFCore.NamingConventions](https://github.com/efcore/EFCore.NamingConventions) for snake_case database naming
- [Isopoh.Cryptography.Argon2](https://github.com/mheyman/Isopoh.Cryptography.Argon2) for password hashing
- JWT bearer authentication via `Microsoft.AspNetCore.Authentication.JwtBearer`
- [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) for Swagger/OpenAPI generation

## Project structure

```
PinballPVP.Api/
├── Controllers/   # API endpoints (Auth, Users, SoloMatches, VersusMatches, PlayerRecords)
├── Models/        # EF Core entities (User, SoloMatch, VersusMatch, PlayerRecord)
├── Dtos/          # Request/response DTOs, grouped by feature (User, Login, Matches, Player Records, Leaderboards)
├── Data/          # PinballPVPContext (EF Core DbContext) and entity configuration
├── Services/      # Application services (password hashing, JWT issuing)
├── Extensions/    # Helper extensions (e.g. reading the authenticated user's id from JWT claims)
└── Migrations/    # EF Core database migrations
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A running [PostgreSQL](https://www.postgresql.org/) instance

### Setup

1. Clone the repository and restore dependencies:

   ```bash
   dotnet restore
   ```

2. Configure your database connection string and JWT settings in `PinballPVP.Api/appsettings.Development.json`
   (or, preferably, via `dotnet user-secrets` so you don't commit them):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=pinballpvp;Username=<user>;Password=<password>"
     },
     "Jwt": {
       "Key": "<a long random signing secret>",
       "Issuer": "PinballPVP.Api",
       "Audience": "PinballPVP.Client",
       "ExpirationMinutes": 60
     }
   }
   ```

3. Apply the database migrations:

   ```bash
   dotnet ef database update --project PinballPVP.Api
   ```

4. Run the API:

   ```bash
   dotnet run --project PinballPVP.Api
   ```

   Or with hot reload during development:

   ```bash
   dotnet watch run --project PinballPVP.Api
   ```

5. Browse the Swagger UI (development only) at `/swagger` — e.g. `http://localhost:5044/swagger`.

## API overview

All endpoints are rooted at `/api`. Routes marked 🔒 require a JWT bearer token (`Authorization: Bearer <token>`),
obtained via `POST /api/auth`.

| Method | Route                              | Auth | Description                                              |
|--------|------------------------------------|:----:|----------------------------------------------------------|
| POST   | `/api/auth`                        |      | Log in with username + password, returns a JWT           |
| GET    | `/api/users`                       |      | List all users                                            |
| GET    | `/api/users/{id}`                  |      | Get a single user                                         |
| POST   | `/api/users`                       |      | Register a new user                                       |
| GET    | `/api/users/playerrecords/{id}`    |      | Get a user's aggregated player record                     |
| GET    | `/api/solomatches`                 |      | List solo matches (optional `?period=week\|month\|year`) |
| GET    | `/api/solomatches/{id}`            |      | Get a single solo match                                   |
| GET    | `/api/solomatches/user/{userId}`   |      | List a user's solo matches                                |
| POST   | `/api/solomatches`                 |  🔒  | Log a new solo match (caller must be the match's player)  |
| GET    | `/api/versusmatches`               |      | List versus matches (optional `?period=week\|month\|year`) |
| GET    | `/api/versusmatches/{id}`          |      | Get a single versus match                                 |
| GET    | `/api/versusmatches/user/{userId}` |      | List a user's versus matches                              |
| POST   | `/api/versusmatches`               |  🔒  | Log a new versus match (caller must be the winner or loser) |

### Authentication

`POST /api/auth` accepts `{ "username": "...", "password": "..." }` and returns `{ "token": "<jwt>" }` on
success. Send that token as `Authorization: Bearer <token>` on subsequent requests to 🔒 endpoints.

The API identifies the caller from the JWT's `sub` claim (the user's id) — protected match-creation
endpoints check that the authenticated user is actually one of the players named in the request body
(e.g. for versus matches, the winner or the loser, since either may act as the P2P host reporting the
result) and respond `403 Forbidden` otherwise.

## Database migrations

Migrations are managed with the EF Core CLI tools (`dotnet ef`):

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project PinballPVP.Api

# Apply pending migrations to the database
dotnet ef database update --project PinballPVP.Api
```
