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

Azure Static Web Apps free (frontend) + App Service F1 (API) + Azure SQL free offer (32 GB; raw GPX/FIT files stored in the DB for now). Deploy via GitHub Actions with path filters. An hourly GitHub Actions cron hits `/sync` — it both wakes the sleeping F1 instance and triggers the Polar pull. Free-tier quirks to respect: F1 has 60 CPU-min/day and cold starts; Azure SQL free offer auto-pauses and stops (not bills) when the monthly grant runs out.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
