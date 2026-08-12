# NileChain API deploy

Startup already runs `Database.MigrateAsync()` — pushing this repo applies pending EF migrations (including W2 deal physics). Do **not** run `--seed-demo` / `SEED_DEMO_ON_STARTUP` on Heroku.

## Heroku (current production)

App: `nilechain-api` → `https://nilechain-api-ee4cc7889a58.herokuapp.com`

```bash
heroku buildpacks:set https://github.com/jincod/dotnetcore-buildpack -a nilechain-api
heroku config:set PROJECT=NileChain.API/NileChain.API.csproj -a nilechain-api
git push heroku HEAD:main
```

Required config vars (never commit values):

| Key | Notes |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Azure SQL / SQL Server |
| `Jwt__Secret` | ≥ 32 chars, not the Development placeholder |
| `Cors__Origins` | Live Angular origin(s), comma-separated |
| `App__FrontendBaseUrl` | Same origin as the SPA (email links + CORS merge) |
| `Chroma__BaseUrl` | Deployed chroma-proxy HTTPS origin |

Optional: `OpenApi__Enabled=true` for a graduation demo. Keep `Payments__GatewayEnabled` / live Paymob off until P1.

Health: `GET /health`

## Docker (Railway / Render / local)

```bash
docker build -t nilechain-api .
docker run --rm -p 8080:8080 -e PORT=8080 -e ASPNETCORE_ENVIRONMENT=Production ...
```

## Frontend

Production Angular (`nilechain-frontend-main`) points at the Heroku API in `environment.prod.ts`. Host the SPA with HTML5 fallback (`public/_redirects` for Netlify/Cloudflare). Set Heroku `Cors__Origins` to that SPA origin before going live.
