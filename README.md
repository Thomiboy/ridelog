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

# Polar AccessLink (from https://admin.polaraccesslink.com)
dotnet user-secrets set "Polar:ClientId" "<polar client id>"
dotnet user-secrets set "Polar:ClientSecret" "<polar client secret>"
# Shared secret the hourly sync cron sends in the X-Sync-Secret header
dotnet user-secrets set "Polar:SyncSharedSecret" "<random string>"
```

Apply the schema with `dotnet ef database update --project ../RideLog.Infrastructure`.
The admin user (`AdminUser:Email`) is seeded on first run. Link Polar by signing in
and visiting `/polar/authorize`; the hourly cron calls `/sync` with the shared secret.

## Deployment

Pushing to `main` runs the CI workflows, which deploy on green:

- **Backend** (`backend-ci.yml`) publishes the API to App Service `ridelog-api` using the
  `AZURE_WEBAPP_PUBLISH_PROFILE` secret, then polls `/health`. EF migrations run at startup
  (`RideLogInitializer` calls `Database.Migrate()`), so there is no separate migration step.

- **Sync cron** (`sync-cron.yml`) calls `/sync` hourly with the `X-Sync-Secret` header from the
  `SYNC_SHARED_SECRET` repo secret — one mechanism that both wakes the sleeping F1 instance and
  pulls new Polar rides. It can also be run manually from the Actions tab (workflow_dispatch).
  The secret's value must equal the `Polar__SyncSharedSecret` App Service setting.

Configure these **App Service application settings** in the Azure portal (double underscore maps
to the config hierarchy) — they are secrets and are never committed:

```
ConnectionStrings__RideLog     = <Azure SQL connection string>
Jwt__SigningKey                = <random string, at least 32 bytes>
AdminUser__Email               = <admin email>
AdminUser__Password            = <initial admin password>
Polar__ClientId                = <polar client id>
Polar__ClientSecret            = <polar client secret>
Polar__SyncSharedSecret        = <random string, shared with the sync cron>
Polar__RedirectUri             = https://<app-default-domain>/polar/callback
Cors__AllowedOrigins__0        = <Static Web App origin, set once the frontend is deployed>
```

> The App Service default domain includes a unique suffix (e.g.
> `ridelog-api-xxxx.polandcentral-01.azurewebsites.net`); use that exact host in
> `Polar__RedirectUri`, whitelist the same callback URL on the Polar client, and keep it in
> sync with `APP_URL` in `backend-ci.yml`.

## Roadmap

- **Phase 1 (MVP):** Polar sync, historical import, ride list + detail with map, basic dashboard, public read-only + admin login, CI/CD
- **Phase 2:** Bryton FIT upload + merge, temperature stats, HR zones, personal records, all-routes map, Hungarian translation
- **Backlog:** multi-user mode, other sports, Google/Microsoft login, containerization (Container Apps), MapLibre, interactive analysis view (linked map + elevation + HR), Azure Blob Storage for raw files, weather enrichment
