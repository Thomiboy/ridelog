---
name: ridelog-phase-plan
description: Agreed phase plan — MVP scope, phase 2, backlog items collected during the grilling session
metadata:
  type: project
---

Agreed 2026-07-16. Phases map to GitHub milestones.

- **Phase 1 (MVP):** Polar AccessLink connect + auto-sync of new sessions · one-time historical GPX/TCX bulk import · ride list + ride detail (Leaflet route map, distance/time/avg-max speed/HR/elevation/cadence) · dashboard (monthly + yearly km, monthly distance and avg-speed trend charts) · public read-only + seeded admin login (Identity + JWT) · CI/CD live on Azure free tiers.
- **Phase 2:** Bryton FIT manual upload + time-overlap merge (temperature enrichment) · temperature stats · HR-zone distribution · personal records (longest ride, most climbing, …) · all-routes overview map · Hungarian translation (Transloco).
- **Backlog (explicitly parked):** multi-user mode · other sports in UI · Google/Microsoft login via Identity external providers · containerization + move to Container Apps (Dockerfile exists from MVP, deploy stays on F1) · MapLibre + terrain map upgrade · ECharts-based interactive analysis view (linked map + elevation profile + HR cursor) · raw files to Azure Blob Storage · weather-API enrichment.

**Why:** MVP is cut to "data flows in automatically and I can see it" — everything analytical lands on live data later, which keeps motivation up.
**How to apply:** when creating issues, put them under the right milestone; resist pulling phase-2 items into MVP. See [[ridelog-architecture-decisions]].
