# PinballPVP API

A REST API for tracking pinball scores and head-to-head matches, built with **ASP.NET Core (.NET 10)** and
**PostgreSQL**. It keeps track of users, their solo and versus matches, and an aggregated player record
(wins, losses and highscores) for each player.

## Features

- **User accounts** with unique usernames, nicknames and emails, and Argon2-hashed passwords.
- **Solo matches** — log a single-player session (final score, rounds won, win/loss) against your own highscore.
- **Versus matches** — log head-to-head results between two users, automatically updating both players' records.
- **Player records** — aggregated solo/versus win-loss counts and highscores per user.
- **Period filters** — list matches filtered by `week`, `month` or `year`.
- **Swagger / OpenAPI** UI for exploring and testing the API in development.

## Tech stack

- [ASP.NET Core 10](https://learn.microsoft.com/aspnet/core) Web API
- [Entity Framework Core](https://learn.microsoft.com/ef/core) with [Npgsql](https://www.npgsql.org/efcore/) (PostgreSQL provider)
- [EFCore.NamingConventions](https://github.com/efcore/EFCore.NamingConventions) for snake_case database naming
- [Isopoh.Cryptography.Argon2](https://github.com/mheyman/Isopoh.Cryptography.Argon2) for password hashing
- [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) for Swagger/OpenAPI generation

## Project structure

```
PinballPVP.Api/
├── Controllers/   # API endpoints (Users, SoloMatches, VersusMatches, PlayerRecords)
├── Models/        # EF Core entities (User, SoloMatch, VersusMatch, PlayerRecord)
├── Dtos/          # Request/response DTOs, grouped by feature (User, Matches, Player Records, Leaderboards)
├── Data/          # PinballPVPContext (EF Core DbContext) and entity configuration
├── Services/      # Application services (e.g. password hashing)
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

2. Configure your database connection string in `PinballPVP.Api/appsettings.Development.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=pinballpvp;Username=<user>;Password=<password>"
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

All endpoints are rooted at `/api`.

| Method | Route                            | Description                                            |
|--------|----------------------------------|--------------------------------------------------------|
| GET    | `/api/users`                     | List all users                                          |
| GET    | `/api/users/{id}`                | Get a single user                                       |
| POST   | `/api/users`                     | Register a new user                                     |
| GET    | `/api/users/playerrecords/{id}`  | Get a user's aggregated player record                   |
| GET    | `/api/solomatches`               | List solo matches (optional `?period=week\|month\|year`) |
| GET    | `/api/solomatches/{id}`          | Get a single solo match                                 |
| GET    | `/api/solomatches/user/{userId}` | List a user's solo matches                              |
| POST   | `/api/solomatches`               | Log a new solo match                                    |
| GET    | `/api/versusmatches`             | List versus matches (optional `?period=week\|month\|year`) |
| GET    | `/api/versusmatches/{id}`        | Get a single versus match                               |
| GET    | `/api/versusmatches/user/{userId}` | List a user's versus matches                          |
| POST   | `/api/versusmatches`             | Log a new versus match between two users                |

## Database migrations

Migrations are managed with the EF Core CLI tools (`dotnet ef`):

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project PinballPVP.Api

# Apply pending migrations to the database
dotnet ef database update --project PinballPVP.Api
```
