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
dotnet build ManufacturingPlatform.sln
dotnet run --project src/backend/Host/ManufacturingPlatform.Host/ManufacturingPlatform.Host.csproj
```

Frontend:

```powershell
cd src/frontend/web
npm install
npm run dev
```

## Next implementation priorities

1. Add persistence, authentication, and configuration to the Platform module.
2. Formalize the shared master-data model across CRM, ERP, MES, and Quality.
3. Implement end-to-end sales order to production order to stock movement flows.
4. Add cost accounting, reporting jobs, and legacy-system integration adapters.
