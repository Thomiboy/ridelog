# RideLog

Personal road-cycling analytics: automatic ride ingestion from Polar, enriched with Bryton data, visualized with statistics, progress charts and route maps.

> **Status:** early development — Phase 1 (MVP) in progress.

## What it does

- **Automatic sync** of training sessions from [Polar AccessLink API](https://www.polar.com/polar-api-v4/) (OAuth2)
- **Manual FIT upload** from a Bryton bike computer — matched to the same ride recorded by Polar and merged in (temperature enrichment)
- **One-time historical import** of past rides via GPX/TCX bulk upload
- **Dashboard** with monthly/yearly totals and progress charts
- **Ride detail view** with the route drawn on an interactive map
- **Public read-only** views; syncing and uploads are admin-only

## Stack

| Layer     | Choice                                                                 |
| --------- | ---------------------------------------------------------------------- |
| Frontend  | Angular, Leaflet + OpenStreetMap, Chart.js (ng2-charts), Transloco i18n |
| Backend   | .NET (current LTS), onion architecture, lightweight CQRS with a hand-rolled dispatcher |
| Auth      | ASP.NET Core Identity + JWT (single seeded admin; multi-user-ready data model) |
| Database  | Azure SQL Database (free offer), EF Core                                |
| Hosting   | Azure Static Web Apps (frontend) + App Service F1 (API), zero-cost tiers |
| CI/CD     | GitHub Actions (path-filtered builds; hourly cron pings `/sync`)        |

## Repository layout

```
/
├── backend/    .NET solution (Domain, Application, Infrastructure, Api)
├── frontend/   Angular app
├── docs/       ADRs, agent configuration
└── .github/    CI/CD workflows
```

## Development

```bash
# backend
cd backend
dotnet build
dotnet test
dotnet run --project src/RideLog.Api

# frontend
cd frontend
npm install
npm start
```

### Backend secrets (local)

The API needs a JWT signing key and an initial admin password, kept out of source
control via user-secrets (issuer/audience, admin email and CORS origin ship in
`appsettings.Development.json`):

```bash
cd backend/src/RideLog.Api
dotnet user-secrets set "Jwt:SigningKey" "<random string, at least 32 bytes>"
dotnet user-secrets set "AdminUser:Password" "<initial admin password>"
```

Apply the schema with `dotnet ef database update --project ../RideLog.Infrastructure`.
The admin user (`AdminUser:Email`) is seeded on first run.

## Roadmap

- **Phase 1 (MVP):** Polar sync, historical import, ride list + detail with map, basic dashboard, public read-only + admin login, CI/CD
- **Phase 2:** Bryton FIT upload + merge, temperature stats, HR zones, personal records, all-routes map, Hungarian translation
- **Backlog:** multi-user mode, other sports, Google/Microsoft login, containerization (Container Apps), MapLibre, interactive analysis view (linked map + elevation + HR), Azure Blob Storage for raw files, weather enrichment
