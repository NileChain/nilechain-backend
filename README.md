# NileChain Backend

ASP.NET Core 10 Web API for **NileChain** — a platform that connects farms and factories with AI matching, contracts, payments, and market insights.

## Solution structure

| Project | Role |
| --- | --- |
| `NileChain.API` | HTTP entry point, middleware, configuration, Swagger |
| `NileChain.Application` | DTOs, services, validators, mappings |
| `NileChain.Domain` | Entities, enums, shared domain models |
| `NileChain.Infrastructure` | EF Core, repositories, external integrations |

Solution file: `NileChain.slnx`

See [docs/ProjectStructure.md](docs/ProjectStructure.md) for the full blueprint and planned API endpoints.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB, Express, or full) when you wire up the database

## Getting started

```bash
git clone https://github.com/<your-user>/nilechain.git
cd nilechain
dotnet restore NileChain.slnx
dotnet build NileChain.slnx
dotnet run --project NileChain.API
```

### URLs

| Resource | URL |
| --- | --- |
| HTTP | http://localhost:5190 |
| HTTPS | https://localhost:7018 |
| Swagger UI | http://localhost:5190/swagger |
| OpenAPI JSON | http://localhost:5190/swagger/v1/swagger.json |

Swagger is enabled in the **Development** environment. Running the `http` or `https` launch profile opens Swagger UI in the browser.

## Configuration

Edit `NileChain.API/appsettings.json`, or use [user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for local overrides:

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection (`NileChainDb`) |
| `Jwt` | Issuer, audience, secret, token lifetimes |
| `Paymob` | Payment gateway credentials |
| `AiServices` | AI / RAG provider settings |
| `Email` / `Sms` | Notification providers |

**Do not commit real secrets.** Keep placeholders in `appsettings.json` and override locally:

```bash
cd NileChain.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-long-random-secret"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=NileChainDb;Trusted_Connection=True;TrustServerCertificate=True"
```

## Tech stack

- .NET 10 / ASP.NET Core Web API
- Swagger / OpenAPI via Swashbuckle.AspNetCore
- Layered architecture: API → Application → Domain / Infrastructure
- Planned: EF Core, JWT auth, Paymob, AI matching & assistant

## Current status

Scaffolded layered solution with configuration placeholders and Swagger. Controllers, domain entities, and infrastructure implementations from the project blueprint are still to be built — see [docs/ProjectStructure.md](docs/ProjectStructure.md).
