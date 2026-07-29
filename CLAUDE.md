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
- **Per-point metric series:** every ride stores a downsampled `MetricSample[]` (≤500 points) built by `MetricSeriesBuilder` — cumulative distance, elapsed minutes, elevation, heart rate, temperature and speed. It is a **JSON column**, not a child table, so adding a channel needs **no EF migration** (old rows deserialize with the new field null) but does need an admin **Reprocess all** to backfill. Speed is source-first: the device's reading (TCX `TPX/Speed`, FIT per-record speed) where the file has one, otherwise derived from position and time on the full track before downsampling.
- **Frontend:** the Leaflet map lives behind a dedicated Angular component so the map engine can be swapped later (MapLibre is on the backlog). There is **one map** — the global background map behind the bottom sheet, driven by `MapState`; pages set what it shows (`showRoute` / `showRoutes` / `showCoverage`) and `reset()` on leaving. Charts use Chart.js via ng2-charts behind a shared `Chart` component. UI strings go through Transloco (English + Hungarian), and numbers/dates through `@jsverse/transloco-locale` (`en-US` / `hu-HU`). Light/dark/system theming via `ThemeService` + Material `light-dark()` tokens.

## Hosting (all zero-cost tiers — keep it that way)

Azure Static Web Apps free (frontend) + App Service F1 (API) + Azure SQL free offer (32 GB; raw GPX/FIT files stored in the DB for now). Deploy via GitHub Actions with path filters. A GitHub Actions cron hits `/sync` — it both wakes the sleeping F1 instance and triggers the Polar pull. Free-tier quirks to respect: F1 has 60 CPU-min/day and cold starts; Azure SQL free offer auto-pauses and stops (not bills) when the monthly grant runs out.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`. **Neither exists yet** — the domain model and decisions currently live in this file and in the issue history; create them when the first one is actually needed.

## Operational notes (learned the hard way)

### Free-tier quota interactions
- **App Service F1**: 60 CPU-min/day, **resets daily ~00:00 UTC**. When exhausted the site returns 403/503 and **backend deploys fail** (the `/health` gate can't pass; the Azure "Deploy to App Service" step hangs then fails). A crash-looping app (e.g. DB unreachable at startup → the initializer throws → App Service restarts it repeatedly) burns this quota fast — a DB outage can drag the API down with it.
- **Azure SQL free offer** (serverless): ~100,000 vCore-seconds/month ≈ ~55 active hours at 0.5 vCore. On the free offer the **auto-pause delay is fixed at 1h and not editable** (no slider under Compute + storage). When the monthly grant runs out the DB **pauses until the next month** (or set the free-offer "limit reached" behaviour to continue-with-billing). Waking the DB frequently keeps serverless compute billing and burns the grant in days.
- **Sync cron cadence matters**: hourly `/sync` kept the serverless DB awake ~24/7 (each wake resets the 1h auto-pause timer) and exhausted the SQL grant within days, which then crash-looped the API and burned the F1 CPU too. Now the cron runs **once a day at 07:00 UTC (~09:00 Europe/Budapest)**; the admin page's manual sync + `workflow_dispatch` cover on-demand pulls.

### Polar AccessLink gotchas
- **GPX/TCX downloads need the file media type in `Accept`** — `application/gpx+xml` / `application/vnd.garmin.tcx+xml`. Sending `application/json` (the default for the JSON endpoints) → **HTTP 406** on the file sub-resources, so every GPS-ride sync failed at download. This was the real reason "sync ran but no ride appeared" (the visible rides had all come from manual import).
- A failed exercise is **logged at error level** (`PolarSyncService`) and the last sync's imported/skipped/failed counts show on the admin Sync card. The transaction is **committed even on failure**, so a lost exercise is **not re-served** — recover it via Polar Flow export → admin Import.

### Dev container quirk
The container ships **Node 22.22.2**, but the Angular CLI's `SUPPORTED_NODE_VERSIONS` starts at `^22.22.3`, so `ng test` / `ng build` refuse to run after a fresh `npm install`. Workaround: relax the range in `frontend/node_modules/@angular/cli/src/utilities/node-version.js` (`'^22.22.3 ...'` → `'^22.22.2 ...'`). `node_modules` is gitignored, so this must be re-applied whenever dependencies are reinstalled.

### Current status (2026-07-29)
- **Phase 1 and Phase 2 are both complete; the GitHub issue list is empty.** Everything through PR #116 is merged to `main`.
- Phase 2 delivered: statistics records and charts, the calendar view, ride comparison, rest markers, dark mode, Hungarian translation, locale-sensitive number/date/duration formatting (#104), two-year comparison on the Trends charts (#110), the Rides coverage backdrop (#113), and the four-channel ride graph with speed (#114).
- **Outstanding manual actions:**
  - Run the admin **"Reprocess all"** so existing rides gain the stored per-point **speed** added in #114 — new syncs already carry it. It burns F1 CPU minutes, so run it after the daily reset (~00:00 UTC).
  - **Import the lost Polar exercise `498528704`** (2026-07-23) via Polar Flow export → admin Import, if it hasn't been done yet (Polar won't re-serve a committed exercise).
- Test suite: **169 backend** (unit + endpoint tests in `RideLog.UnitTests`) and **257 frontend** (Vitest, 36 files).
- Working agreement with the owner: develop on `develop`, TDD via `/tdd #N`, and **only push and open a PR (develop → main) when the owner says so** — they merge it themselves.
