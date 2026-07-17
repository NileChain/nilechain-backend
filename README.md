# NileChain Backend

ASP.NET Core 8 Web API for NileChain — a platform that connects farms and factories with AI matching, contracts, payments, and market insights.

## Solution structure

| Project | Role |
| --- | --- |
| `NileChain.API` | HTTP entry point, controllers, middleware, configuration |
| `NileChain.Application` | DTOs, services, validators, mappings |
| `NileChain.Domain` | Entities, enums, shared domain models |
| `NileChain.Infrastructure` | EF Core, repositories, external integrations |

See [docs/ProjectStructure.md](docs/ProjectStructure.md) for the full blueprint and planned API endpoints.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB, Express, or full) for the default connection string

## Getting started

```bash
git clone <your-repo-url>
cd backend
dotnet restore NileChain.slnx
dotnet build NileChain.slnx
dotnet run --project NileChain.API
```

By default the API listens on:

- `https://localhost:7018`
- `http://localhost:5190`

## Configuration

Edit `NileChain.API/appsettings.json` (or use [user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) in Development):

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection |
| `Jwt` | Issuer, audience, secret, token lifetimes |
| `Paymob` | Payment gateway credentials |
| `AiServices` | AI / RAG provider settings |
| `Email` / `Sms` | Notification providers |

**Do not commit real secrets.** Keep placeholders in `appsettings.json` and override locally via user secrets or environment variables:

```bash
cd NileChain.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-long-random-secret"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=NileChainDb;Trusted_Connection=True;TrustServerCertificate=True"
```

## Tech stack

- .NET 8 / ASP.NET Core Web API
- Layered architecture (API → Application → Domain / Infrastructure)
- Planned: EF Core, JWT auth, Paymob, AI matching & assistant
