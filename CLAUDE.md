# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

RideLog — personal road-cycling analytics site. Automatic ride ingestion from the Polar AccessLink API, optional manual Bryton FIT uploads (temperature enrichment), statistics, progress charts and Leaflet route maps. Single-user (admin) with public read-only views; the data model is multi-user-ready (`UserId` on every user-owned entity). Portfolio project — code quality, README and issue history are part of the product.

## Commands

```bash
# backend (from backend/)
dotnet build
dotnet test
dotnet run --project src/RideLog.Api

# frontend (from frontend/)
npm install
npm start          # dev server
npm test
npm run build
```

## Architecture

- **Monorepo:** `backend/` (.NET solution) + `frontend/` (Angular app).
- **Backend:** onion architecture, exactly four projects — `RideLog.Domain`, `RideLog.Application`, `RideLog.Infrastructure`, `RideLog.Api`. Lightweight CQRS: `ICommandHandler<T>` / `IQueryHandler<T,TResult>` with a **hand-rolled dispatcher** (no MediatR — it went commercial; do not add it). Cross-cutting pipeline behaviors via DI decorators.
- **No generic repository over EF Core.** The query side uses EF Core projections directly to DTOs behind interfaces defined in Application. Mapping is manual or via Mapperly (no AutoMapper).
- **Data sources:** Polar AccessLink is the primary, automatic source (only delivers sessions created after client registration — historical rides come from one-time GPX/TCX bulk upload). Bryton has **no public API**; its FIT files are uploaded manually and must be **merged into the matching Polar ride** (matched by time overlap), never stored as duplicate rides. All sports are stored raw; the UI shows cycling only.
- **Auth:** ASP.NET Core Identity + JWT bearer (frontend and API are on different origins — no cookies). One seeded admin user. Read endpoints are public.
- **Frontend:** the Leaflet map lives behind a dedicated Angular component so the map engine can be swapped later (MapLibre is on the backlog). Charts use Chart.js via ng2-charts. UI strings go through Transloco (English now, Hungarian later).

## Hosting (all zero-cost tiers — keep it that way)

Azure Static Web Apps free (frontend) + App Service F1 (API) + Azure SQL free offer (32 GB; raw GPX/FIT files stored in the DB for now). Deploy via GitHub Actions with path filters. A GitHub Actions cron hits `/sync` — it both wakes the sleeping F1 instance and triggers the Polar pull. Free-tier quirks to respect: F1 has 60 CPU-min/day and cold starts; Azure SQL free offer auto-pauses and stops (not bills) when the monthly grant runs out.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Operational notes (learned the hard way)

### Free-tier quota interactions
- **App Service F1**: 60 CPU-min/day, **resets daily ~00:00 UTC**. When exhausted the site returns 403/503 and **backend deploys fail** (the `/health` gate can't pass; the Azure "Deploy to App Service" step hangs then fails). A crash-looping app (e.g. DB unreachable at startup → the initializer throws → App Service restarts it repeatedly) burns this quota fast — a DB outage can drag the API down with it.
- **Azure SQL free offer** (serverless): ~100,000 vCore-seconds/month ≈ ~55 active hours at 0.5 vCore. On the free offer the **auto-pause delay is fixed at 1h and not editable** (no slider under Compute + storage). When the monthly grant runs out the DB **pauses until the next month** (or set the free-offer "limit reached" behaviour to continue-with-billing). Waking the DB frequently keeps serverless compute billing and burns the grant in days.
- **Sync cron cadence matters**: hourly `/sync` kept the serverless DB awake ~24/7 (each wake resets the 1h auto-pause timer) and exhausted the SQL grant within days, which then crash-looped the API and burned the F1 CPU too. Now the cron runs **once a day at 07:00 UTC (~09:00 Europe/Budapest)**; the admin page's manual sync + `workflow_dispatch` cover on-demand pulls.

### Polar AccessLink gotchas
- **GPX/TCX downloads need the file media type in `Accept`** — `application/gpx+xml` / `application/vnd.garmin.tcx+xml`. Sending `application/json` (the default for the JSON endpoints) → **HTTP 406** on the file sub-resources, so every GPS-ride sync failed at download. This was the real reason "sync ran but no ride appeared" (the visible rides had all come from manual import).
- A failed exercise is **logged at error level** (`PolarSyncService`) and the last sync's imported/skipped/failed counts show on the admin Sync card. The transaction is **committed even on failure**, so a lost exercise is **not re-served** — recover it via Polar Flow export → admin Import.

### Current status (2026-07-23)
- PR #75 (Polar 406 `Accept`-header fix) is **merged and deployed live** (the backend deploy was re-run successfully after the F1 quota reset). The full stack now runs the latest code, so the daily/manual Polar sync should import GPS rides.
- Still to do manually: **import the lost Polar exercise `498528704`** (2026-07-23) via Polar Flow export → admin Import (Polar won't re-serve a committed exercise).
- Phase 1 is complete. Phase 2 backlog lives in the **"Phase 2 - Enrichment"** milestone: #44 (dashboard: 3 longest routes), #60 (elevation/HR graph — needs a stored per-point series, currently only the encoded lat/lng polyline is kept), #61 (compare with an earlier ride), #63 (dark mode).
