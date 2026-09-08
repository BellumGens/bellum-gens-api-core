![.NET Core](https://github.com/BellumGens/bellum-gens-api-core/workflows/.NET%20Core/badge.svg)

# Bellum Gens API

ASP.NET Core Web API powering the [Bellum Gens](https://bellumgens.com) esports tournament platform. It provides backend services for tournament management, team organization, player profiles, strategy sharing, and the merchandise shop — with integrations to Steam, Battle.net, Twitch and Revolut.

## Features

- **Tournament management** — brackets, groups, matches, registration, and check-in for CS:GO and StarCraft II
- **Team management** — rosters, invitations, applications, availability scheduling, and map pools
- **Player profiles** — linked Steam / Battle.net / Twitch accounts with live stats
- **Strategy sharing** — create, vote, and comment on CS:GO strategies
- **Search** — find players and teams by name, role, or playstyle overlap
- **Shop** — product catalog with variants and stock, server-side cart pricing, promo codes, Revolut hosted checkout with signed webhooks, order administration
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
| Payments | Revolut Merchant API (hosted checkout + webhooks) |
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
| `BlobService:ShopContainer` | Blob container for product images |
| `shop:*` | Shop settings: `currency`, `shippingCost`, `freeShippingThreshold`, `paymentExpiryMinutes`, `maxQuantityPerLine`, `storefrontUrl`, `reconcileAfterSeconds` |
| `revolut:baseUrl` | `https://sandbox-merchant.revolut.com` or `https://merchant.revolut.com` |
| `revolut:apiVersion` | Merchant API version header, currently `2024-09-01` |
| `revolut:secretKey` | Merchant API secret key. **Secret** — App Service settings or `dotnet user-secrets` only |
| `revolut:webhookSigningSecret` | The `wsk_` secret returned by `POST api/shopadmin/payments/webhook`. **Secret** |

Secrets must never be committed to `appsettings.json`. Locally use `dotnet user-secrets set "revolut:secretKey" "sk_..."`; in Azure use the App Service configuration.

### Shop payment flow

1. `POST api/shop/orders` validates and prices the cart on the server, reserves stock and creates a Revolut order. The response carries the `checkoutUrl` the storefront redirects to.
2. Revolut calls `POST api/shop/webhooks/revolut`. The request is authenticated with the HMAC-SHA256 signature over `v1.{timestamp}.{body}`; `ORDER_COMPLETED` marks the order paid and sends the confirmation email.
3. `GET api/shop/orders/{id}` serves the result page and, if the webhook is late, reconciles the payment with Revolut directly.
4. `PaymentExpirySweeper` cancels unpaid orders after `shop:paymentExpiryMinutes` and releases their stock.

Register the webhook once per environment (after configuring `revolut:secretKey`) by calling `POST api/shopadmin/payments/webhook` with `{ "url": "https://api.bellumgens.com/api/shop/webhooks/revolut" }` as an admin, then store the returned `signingSecret` as `revolut:webhookSigningSecret`.

## Project structure

```
BellumGens.Api.Core/           # Main Web API project
├── Controllers/               # API controllers
├── Models/                    # EF Core entities and view models (Models/Shop for the catalog and orders)
├── Providers/                 # External service integrations (Providers/Payments, Providers/Shop)
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
| `ShopController` | Storefront: catalog, promo lookup, cart quotes, order creation and status, payment retry |
| `ShopAdminController` | Catalog, order, promo and payment administration (admin role) |
| `ShopWebhooksController` | Revolut webhook receiver (signature authenticated) |
| `StrategyController` | CS:GO strategy CRUD, voting, comments |
| `TeamsController` | Team CRUD, roster, invites, availability |
| `TournamentController` | Tournament lifecycle, brackets, matches |
| `UsersController` | Player profiles, stats, availability |

## Testing

The solution includes an xUnit test project covering every controller plus the shop services (pricing, webhooks, the expiry sweeper). Tests use EF Core InMemory and [Moq](https://github.com/moq/moq4) for isolation.

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
