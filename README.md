# RideLog

Personal road-cycling analytics: automatic ride ingestion from Polar, enriched with Bryton data, visualized with statistics, progress charts and route maps.

> **Status:** live — Phase 1 (MVP) complete; Phase 2 (enrichment) in progress.

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

# Sign-in providers (see "Registering the sign-in providers" below)
dotnet user-secrets set "ExternalSignIn:Providers:google:ClientId" "<google client id>"
dotnet user-secrets set "ExternalSignIn:Providers:google:ClientSecret" "<google client secret>"
dotnet user-secrets set "ExternalSignIn:Providers:microsoft:ClientId" "<microsoft application id>"
dotnet user-secrets set "ExternalSignIn:Providers:microsoft:ClientSecret" "<microsoft client secret>"
```

Only the credentials are configured: each provider's authorize and token endpoints ship as
defaults in code, and `ExternalSignIn:RedirectUriTemplate` is already set for local development
in `appsettings.Development.json`.

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

ExternalSignIn__RedirectUriTemplate             = https://<app-default-domain>/auth/{provider}/callback
ExternalSignIn__Providers__google__ClientId     = <google client id>
ExternalSignIn__Providers__google__ClientSecret = <google client secret>
ExternalSignIn__Providers__microsoft__ClientId     = <microsoft application id>
ExternalSignIn__Providers__microsoft__ClientSecret = <microsoft client secret>
```

`{provider}` is a literal placeholder, not something to substitute — the app fills it in per
provider. A provider with no client id is treated as not configured and answers 404 to
`/auth/<provider>/authorize`, so the login page's button is dead until both values are set: the
frontend does not ask the API which providers are available.

> The App Service default domain includes a unique suffix (e.g.
> `ridelog-api-xxxx.polandcentral-01.azurewebsites.net`); use that exact host in
> `Polar__RedirectUri`, whitelist the same callback URL on the Polar client, and keep it in
> sync with `APP_URL` in `backend-ci.yml`.

### Registering the sign-in providers

New riders sign in with Google or Microsoft; there are no local passwords, because nothing here
sends email (ADR 0007). Both consoles need the **exact** callback URL whitelisted — the app sends
it as `redirect_uri`, and a mismatch fails at the provider before reaching RideLog.

**Google** — [Cloud Console](https://console.cloud.google.com/apis/credentials) → *Credentials* →
*Create credentials* → *OAuth client ID* → **Web application**:

```
Authorised redirect URI   https://<app-default-domain>/auth/google/callback
                          https://localhost:7016/auth/google/callback   (for local development)
```

The OAuth consent screen must be configured too. While it is in *Testing*, only the accounts
listed as test users can sign in — the first symptom of forgetting this is a consent screen that
refuses the rider, not an error from RideLog.

**Microsoft** — [Entra ID](https://entra.microsoft.com) → *App registrations* → *New registration*:

```
Supported account types    Accounts in any organizational directory and personal Microsoft accounts
Redirect URI (Web)         https://<app-default-domain>/auth/microsoft/callback
                           https://localhost:7016/auth/microsoft/callback   (for local development)
```

Then *Certificates & secrets* → *New client secret*; the **Value** is the client secret (not the
Secret ID), and it is shown only once. The application (client) ID is the client id.

Microsoft's id_token carries no `email_verified` claim at all, so RideLog treats an absent claim
as verified for Microsoft and as *unverified* for a provider that does send one. It does need an
`email` claim: the app requests the `email` scope, and for a work or school account whose mail
attribute is unset the claim can still be missing — such a sign-in is refused rather than guessed.

A rider who signs in with both providers using the same verified address reaches the same account:
Identity is configured with `RequireUniqueEmail`, so the second provider attaches to the rider who
already holds that address.

## Roadmap

- **Phase 1 (MVP) ✅:** Polar sync, historical import, ride list + detail with map, basic dashboard, public read-only + admin login, CI/CD
- **Phase 2 (feature-complete, polishing):** Bryton FIT upload + merge, temperature stats, HR zones, personal records, calendar view, ride comparison, rest markers, the all-routes coverage backdrop, dark mode, Hungarian translation with locale-aware number/date formatting, per-point metric graph (elevation, heart rate, temperature, speed) with a two-channel picker, and year-over-year comparison on the Trends charts
- **Phase 3 (in progress):**
  - **Interactive analysis view ✅** — the ride graph and the map read each other: scrubbing the chart marks the route, and pointing at the route moves the graph
  - **Weather enrichment ✅** — hourly wind, temperature and conditions per ride from Open-Meteo's archive, with the wind resolved against the direction actually ridden
  - **Other activities ✅** — the non-cycling recordings that were always stored now have their own list beside the rides, and open on the same detail page
  - **Multi-user mode + Google/Microsoft login ✅** — reads are scoped to a rider, new riders sign in with Google or Microsoft, each links their own Polar account, and each tends their own log and can close their account
- **Backlog:** containerization (Container Apps), Azure Blob Storage for raw files — both deferred while the zero-cost hosting setup is working; swapping the map engine for MapLibre, deferred until something actually needs vector styling or a tilted view (#122)
