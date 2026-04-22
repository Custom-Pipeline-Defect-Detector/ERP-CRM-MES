# Manufacturing Platform Scaffold

This repository contains the initial scaffold for an on-premise manufacturing platform covering CRM, ERP, MES, Quality, Reporting, AI, Platform Administration, and Integration.

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

## Next implementation priorities

1. Add persistence, authentication, and configuration to the Platform module.
2. Formalize the shared master-data model across CRM, ERP, MES, and Quality.
3. Implement end-to-end sales order to production order to stock movement flows.
4. Add cost accounting, reporting jobs, and legacy-system integration adapters.
