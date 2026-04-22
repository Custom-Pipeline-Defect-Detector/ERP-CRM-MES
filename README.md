# Manufacturing Platform Scaffold

This repository contains the initial scaffold for an on-premise manufacturing platform covering CRM, ERP, MES, Quality, Reporting, AI, Platform Administration, and Integration.

The current implementation is a persisted phase-1 vertical slice:
- EF Core-backed backend with SQLite for development and SQL Server configuration for production
- CRM to ERP to MES order flow
- purchase orders and goods receipts into inventory
- inventory postings, quality hold/release, work-order costing, customer shipment, and accounting bridge generation/progression
- AI management summary over governed reporting datasets with local fallback when the external gateway is unavailable

## Structure

- `docs`: project blueprint and BRD/SRS draft
- `src/backend`: ASP.NET Core host, shared building blocks, and business modules
- `src/frontend/web`: React + TypeScript intranet shell
- `infra`: deployment, database, and automation placeholders
- `tests`: backend, frontend, and integration test placeholders

## Local commands

Backend:

```powershell
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_HOME="$PWD\\.dotnet_cli"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='0'
$env:DOTNET_NOLOGO='1'
$env:NUGET_PACKAGES="$env:USERPROFILE\\.nuget\\packages"

dotnet build src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj -m:1 -p:RestoreIgnoreFailedSources=true
dotnet run --project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj --urls http://127.0.0.1:5099
```

Backend endpoints:

- `http://127.0.0.1:5099/`
- `http://127.0.0.1:5099/api/health`
- `http://127.0.0.1:5099/swagger`

Frontend:

```powershell
cd src/frontend/web
npm install
npm run build
npm run preview -- --host 127.0.0.1 --port 4173
```

Frontend endpoint:

- `http://127.0.0.1:4173/`

## Persistence

- Development uses SQLite at `infra/data/manufacturing-platform.db`
- Production settings default to SQL Server in `src/backend/Host/ManufacturingPlatform.Host/appsettings.json`
- EF migrations are committed under `src/backend/Host/ManufacturingPlatform.Host/Persistence/Migrations`
- On startup, clean environments apply migrations automatically; older local databases created before migrations continue to start through the compatibility path

Migration commands:

```powershell
dotnet dotnet-ef migrations list --project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj --startup-project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj
dotnet dotnet-ef database update --project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj --startup-project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj
```

## AI Gateway

- Base URL is configured in `appsettings.json`
- Keep the API key out of source control and provide it through environment variables when needed
- OpenAI-compatible gateways should point to the API root, for example `https://host/v1`
- The runtime posts to `chat/completions` when the configured base URL already ends in `/v1`
- `Manufacturing:Ai:TimeoutSeconds` controls how long the backend waits before falling back to local rules

Example:

```powershell
$env:Manufacturing__Ai__BaseUrl='https://api123.icu/v1'
$env:Manufacturing__Ai__ApiKey='your-key-here'
dotnet run --project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj --urls http://127.0.0.1:5099
```

If the gateway is unavailable, times out, or returns an unsupported response, the AI module falls back to deterministic local summaries instead of failing the page.

## Demo Users

- `admin`
- `sales`
- `planner`
- `operator`
- `quality`

## Next implementation priorities

1. Replace demo session login with real authentication, password policy, and RBAC administration.
2. Add purchase, warehouse transfer, shipment, and finance settlement flows on top of the same persisted model.
3. Formalize legacy integration adapters for Kingdee and the existing factory system.
4. Expand reporting into exportable datasets, scheduled jobs, and drill-down management dashboards.
