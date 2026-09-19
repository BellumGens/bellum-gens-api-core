![.NET Core](https://github.com/BellumGens/bellum-gens-api-core/workflows/.NET%20Core/badge.svg)

# Bellum Gens API

ASP.NET Core Web API powering the [Bellum Gens](https://bellumgens.com) esports tournament platform. It provides backend services for tournament management, team organization, player profiles, strategy sharing, and an online jersey shop — with integrations to Steam, Battle.net, and Twitch.

## Features

- **Tournament management** — brackets, groups, matches, registration, and check-in for CS:GO and StarCraft II
- **Team management** — rosters, invitations, applications, availability scheduling, and map pools
- **Player profiles** — linked Steam / Battle.net / Twitch accounts with live stats
- **Strategy sharing** — create, vote, and comment on CS:GO strategies
- **Search** — find players and teams by name, role, or playstyle overlap
- **Shop** — jersey ordering with promo code support
- **Push notifications** — Web Push (RFC 8030) for real-time alerts

## Tech stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10.0 |
| Database | SQL Server + Entity Framework Core 10 |
| Auth | ASP.NET Identity, Steam OpenID, Battle.net OAuth, Twitch OAuth |
| Storage | Azure Blob Storage |
| Notifications | WebPush |
| Email | SMTP (Office 365) |
| CI | GitHub Actions |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB works for development) — not needed on macOS/Linux, see [Local database provider](#local-database-provider)

### Run locally

```bash
# Restore dependencies
dotnet restore

# Apply EF Core migrations
dotnet ef database update --project BellumGens.Api.Core

# Start the API
dotnet run --project BellumGens.Api.Core
```

### Configuration

The API reads settings from `appsettings.json` / `appsettings.Development.json`. You will need to supply your own values for:

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `SteamApiKey` | [Steam Web API key](https://steamcommunity.com/dev/apikey) |
| Battle.net client ID / secret | Battle.net OAuth credentials |
| Twitch client ID / secret | Twitch OAuth credentials |
| Azure Storage connection string | Blob storage for images and strategies |
| VAPID keys | Web Push VAPID key pair |
| SMTP credentials | Email sending via Office 365 |

### Local database provider

The API runs on SQL Server by default. Because LocalDB is Windows-only, the `Database:Provider` setting can switch the
app to a local SQLite file for development on machines without SQL Server:

| `Database:Provider` | Behaviour |
|---|---|
| `SqlServer` (default when unset) | Uses `ConnectionStrings:DefaultConnection` with EF Core migrations (`Database.Migrate()`) |
| `Sqlite` | Uses `ConnectionStrings:DefaultConnection` as a SQLite file, created from the current model (`Database.EnsureCreated()`) |

Keep this setting out of the committed `appsettings*.json` files so each machine can differ. Configure it per machine
with [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), which are loaded automatically in the
Development environment and override `appsettings.Development.json`:

```bash
# macOS / Linux — use a local SQLite file
cd BellumGens.Api.Core
dotnet user-secrets set "Database:Provider" "Sqlite"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=bellumgens.dev.db"
```

On Windows, set no secrets and the app falls back to the SQL Server settings in `appsettings.json` — run
`dotnet ef database update` as usual. The SQLite path needs no migration step; the schema is created on first start,
and the `*.dev.db` file is gitignored.

To check or undo the local override:

```bash
dotnet user-secrets list             # show the active overrides
dotnet user-secrets remove "Database:Provider"   # fall back to SQL Server
```

The SQLite provider ships native binaries for every platform it supports (~33 MB), so its package reference and the
`SQLITE_PROVIDER` compile constant are limited to `Debug` builds and excluded from Release publishes. To build Release
against SQLite anyway:

```bash
dotnet build -c Release -p:IncludeSqliteProvider=true
```

## Project structure

```
BellumGens.Api.Core/           # Main Web API project
├── Controllers/               # API controllers (11 total)
├── Models/                    # EF Core entities and view models
├── Providers/                 # External service integrations
├── Migrations/                # EF Core database migrations
├── Configs/                   # CORS and app configuration
└── Program.cs                 # Host, service and middleware registration

BellumGens.Api.Core.Tests/     # xUnit test project
├── TestUtils.cs               # Shared test helpers and mocks
└── *ControllerTests.cs        # Per-controller test classes
```

### Controllers

| Controller | Description |
|---|---|
| `AccountController` | Authentication, profile management, notifications |
| `AdminController` | Role and user administration, tournament ops |
| `CompaniesController` | Company / sponsor CRUD |
| `HomeController` | Root redirect |
| `PushController` | Web Push subscription management |
| `SearchController` | Player and team search |
| `ShopController` | Jersey orders and promo codes |
| `StrategyController` | CS:GO strategy CRUD, voting, comments |
| `TeamsController` | Team CRUD, roster, invites, availability |
| `TournamentController` | Tournament lifecycle, brackets, matches |
| `UsersController` | Player profiles, stats, availability |

## Testing

The solution includes an xUnit test project with 32 tests covering all 11 controllers. Tests use EF Core InMemory and [Moq](https://github.com/moq/moq4) for isolation.

```bash
# Run all tests
dotnet test

# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### CI

Every push and pull request to `master` triggers the GitHub Actions workflow which builds, tests, collects code coverage, and publishes a coverage report to the job summary.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
