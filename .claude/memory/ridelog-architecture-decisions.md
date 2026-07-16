---
name: ridelog-architecture-decisions
description: Core architecture and hosting decisions from the 2026-07-16 grilling session — data sources, auth, stack, zero-cost Azure hosting
metadata:
  type: project
---

Decisions locked in the 2026-07-16 grilling session with the user (all confirmed one by one):

- **Data sources:** Polar AccessLink API is the sole automatic source (user always records every ride with both devices). Bryton FIT files are manual, optional uploads used only to enrich the matching Polar ride with temperature — matched by time overlap, never stored as a duplicate. Strava-as-hub was **rejected** because Strava API access requires a paid subscription since 2026-06-30. AccessLink only serves sessions recorded after client registration, so historical rides need a one-time GPX/TCX bulk import.
- **Users/auth:** single-user MVP, multi-user-ready schema (`UserId` everywhere). ASP.NET Core Identity + JWT, one seeded admin; public read-only views. Google/Microsoft external login is backlog.
- **Purpose:** personal tool **and** portfolio piece (public repo, readable issue history, polished README).
- **Scope:** all sports stored raw, UI cycling-only.
- **Backend:** onion, exactly 4 projects, lightweight CQRS with hand-rolled dispatcher (MediatR rejected — commercial since v13; AutoMapper likewise, use manual mapping/Mapperly). No generic repository; query side = EF projections.
- **Frontend:** Angular + Leaflet/OSM (map behind own component, MapLibre backlog) + Chart.js (ng2-charts) + Transloco (English first, Hungarian backlog). User has prior Leaflet experience.
- **Hosting (zero-cost):** Azure Static Web Apps free + App Service F1 + Azure SQL free offer (raw files in DB; Blob Storage backlog). GitHub Actions CI/CD; hourly Actions cron pings `/sync` (wake + sync). User had **no Azure account** as of 2026-07-16 — signup (needs a bank card) is a task he does himself.
- **Workflow:** public monorepo `ridelog` (backend/ + frontend/), tasks as GitHub Issues with per-phase milestones, default triage labels.

**Why:** every choice was filtered through "free or nearly free" + "portfolio credibility for a .NET/Azure job market".
**How to apply:** don't introduce paid dependencies or paid tiers; don't add MediatR/AutoMapper; keep the 4-project structure; see [[ridelog-phase-plan]] for scope ordering.
