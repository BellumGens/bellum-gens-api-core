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
- SQL Server (LocalDB works for development)

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

## Project structure

```
BellumGens.Api.Core/           # Main Web API project
├── Controllers/               # API controllers (11 total)
├── Models/                    # EF Core entities and view models
├── Providers/                 # External service integrations
├── Migrations/                # EF Core database migrations
├── Configs/                   # CORS and app configuration
└── Startup.cs                 # Service and middleware registration

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
