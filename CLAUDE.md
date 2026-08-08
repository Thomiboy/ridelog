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
- **Data sources:** Polar AccessLink is the primary, automatic source (only delivers sessions created after client registration — historical rides come from one-time GPX/TCX bulk upload). Bryton has **no public API**; its FIT files are uploaded manually and must be **merged into the matching Polar ride** (matched by time overlap), never stored as duplicate rides. All sports are stored raw. The **`Ride` entity holds every sport, not just cycling** — the name predates the distinction and is a known misnomer (docs/adr/0004); a **Ride** means a cycling one, and everything else is an **Other activity**. Sport is a category *derived* from the raw string at read time, never stored, and a name we don't recognise counts as cycling because untagged historical imports are rides. The API sends that reading (`sportCategory`) rather than leaving the frontend to repeat the table, and **enums travel as names** — an ordinal would tie the wire format to declaration order. One detail page serves both kinds: its back-link and neighbours follow the *list* a recording is in (runs, walks and swims share one), while the comparison picker narrows to the same *kind*.
- **Auth:** ASP.NET Core Identity + JWT bearer (frontend and API are on different origins — no cookies). One seeded admin user. Read endpoints are public.
- **Per-point metric series:** every ride stores a downsampled `MetricSample[]` (≤500 points) built by `MetricSeriesBuilder` — cumulative distance, elapsed minutes, elevation, heart rate, temperature and speed. It is a **JSON column**, not a child table, so adding a channel needs **no EF migration** (old rows deserialize with the new field null) but does need an admin **Reprocess all** to backfill. Speed is source-first: the device's reading (TCX `TPX/Speed`, FIT per-record speed) where the file has one, otherwise derived from position and time on the full track before downsampling.
- **Sign-in is delegated** (#157, docs/adr/0007): no local passwords for new riders, because nothing in this codebase sends email, so there could be no verification and no reset. The seeded admin keeps its password. `RequireUniqueEmail` also settles account linking — Identity cannot make a second account for an address that exists, so `ExternalSignIn` attaches a second provider to the rider who already holds the address, and refuses an address the provider did not verify. The round trip is written like the Polar one (data-protected state, no cookie) rather than through Identity's external-login plumbing, which keeps correlation in a cookie this cross-origin app does not have. The callback hands the browser a **single-use code**, not a token — the frontend exchanges it at `/auth/exchange`, so the JWT never lands in browser history. Codes live in memory: spending one is a fact about the server, not about the code.
- **Polar is linked per rider** (#158). Every `IPolarTokenStore` read names its rider, and `IPolarClient` takes the link to pull with — it used to fetch "the" connection itself and got whichever row came first, so a second rider's sync would have pulled the *first* rider's exercises and stored them as their own. The daily cron run (`/sync` with the shared secret) covers **every** linked rider and isolates each one: a failed pull or a weather outage is recorded against that rider and the run carries on. A rider syncing themselves (`/sync` with a JWT) still syncs only their own log.
- **Maintenance is already rider-scoped, despite how it reads.** Every `IRideMaintenanceService` method takes a user and filters on it, so "reprocess all" and "delete all" mean *this rider's* rides — they only look global because there is one rider. #159 therefore opens them to every rider rather than keeping them behind the admin role.
- **Reads name the rider they are for** (#156, docs/adr/0006). Every read query carries a `RiderId` and every handler filters on it; the endpoint resolves it as the signed-in rider, or the one configured **public log**. That setting defaults to the seeded admin at startup — leaving it unset would otherwise blank the public site, which is how it was nearly shipped.
- **Frontend:** the Leaflet map lives behind a dedicated Angular component — `RouteMap` is the only thing that touches a map, and everything engine-facing is ~240 lines. That keeps an engine swap cheap, but it isn't a drop-in port: `LeafletApi` is a structural slice of Leaflet's own module type, so a different engine means rewriting that type rather than re-implementing it (see #122, closed). There is **one map** — the global background map behind the bottom sheet, driven by `MapState`; pages set what it shows (`showRoute` / `showRoutes` / `showCoverage`) and `reset()` on leaving. Charts use Chart.js via ng2-charts behind a shared `Chart` component. UI strings go through Transloco (English + Hungarian), and numbers/dates through `@jsverse/transloco-locale` (`en-US` / `hu-HU`). Light/dark/system theming via `ThemeService` + Material `light-dark()` tokens.

## Hosting (all zero-cost tiers — keep it that way)

Azure Static Web Apps free (frontend) + App Service F1 (API) + Azure SQL free offer (32 GB; raw GPX/FIT files stored in the DB for now). Deploy via GitHub Actions with path filters. A GitHub Actions cron hits `/sync` — it both wakes the sleeping F1 instance and triggers the Polar pull. Free-tier quirks to respect: F1 has 60 CPU-min/day and cold starts; Azure SQL free offer auto-pauses and stops (not bills) when the monthly grant runs out.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`. `CONTEXT.md` is a **glossary only** — the canonical term for each concept, with the rejected synonyms listed under `_Avoid_`; keep implementation detail out of it. ADRs are added sparingly, only for decisions that are hard to reverse, surprising without context, and the result of a real trade-off.

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

### Open-Meteo archive (measured, not assumed)
Two things were checked against real responses rather than taken on trust, both in
`backend/tests/RideLog.UnitTests/Weather/Fixtures/`:
- **The archive is not days behind.** A probe over 2026-07-24 → 07-30, run on 07-31, came back complete
  with no nulls — data existed through the previous day. The "ERA5 lags ~5 days" assumption that shaped
  the original plan was wrong. (Recent hours may still be preliminary and get revised later; unverified.)
- **Hourly timestamps carry no offset** (`"2024-05-05T06:00"`) even with `timezone=UTC` requested and
  `utc_offset_seconds: 0` in the body. Parsed as local time they shift by the running machine's zone and
  still look plausible — `OpenMeteoResponseReader` pins them to UTC, and a test asserts it.
- **The response snaps to a grid cell**: asking for 46.24613/20.14038 answered 46.291737/20.127794, ~5 km
  away, with its own `elevation`. Reported for an area, not measured at the handlebars (docs/adr/0005).

### Headwind is resolved per sample, not per hour
Wind is reported hourly; a rider's direction changes with the road, and the two do not line up. Taking
an hour's direction from its end points asks which way someone went when they went out and came back —
on the 2025-05-31 fixture (62 km, turnaround at 31 km, 07:27Z) that answered "tailwind" for an hour
that honestly weighed was neither, and called the run home a headwind when it was pushed along by most
of a 6.9 km/h wind. `RideWeatherReader` therefore resolves each **sample** against its own hour's wind,
and an hour's figure is the distance-weighted mean of its own samples. Both fixtures
(`Weather/Fixtures/open-meteo-2025-05-31-szeged.json` and the 2024-05-05 one) are real responses.

### Derived metrics come from real files, not invented fixtures
`backend/tests/RideLog.UnitTests/Import/Fixtures/` holds five real exports (four Polar TCX, one Bryton FIT), committed because hand-made fixtures kept agreeing with whatever the code happened to do. **Reach for them first** when a derived number looks wrong — they carry GPS warm-up, stale repeated positions, positionless records and dishonest device summaries, none of which anyone thinks to invent. Five rounds of fixing top speed from the symptom alone were undone in one sitting once a real file was on disk.

Two things they settled, both in `docs/adr/0003` (which supersedes `0002`):
- **A device's own summary beats anything we derive from GPS positions** — four of the five devices summarise a believable maximum while four of five derivations are wrong. The track may only *veto* a summary it cannot support at all.
- **The graph and the top-speed figure are different computations on purpose.** `SpeedSeries.Resolve` answers "how fast could the rider have been going here" (an upper bound, feeding the veto); `SpeedSeries.ForGraph` answers "what did the ride look like" (windowed and capped, feeding the chart). Everywhere else in this codebase two paths to one number has been a bug — here it is the design, so check the names before "fixing" it.

### Current status (2026-07-31)
- **Phase 1 is complete; Phase 2 is feature-complete but deliberately still open** — the owner is reviewing what to fine-tune, so new polish issues land in the Phase 2 milestone rather than Phase 3.
- **Phase 3 is planned** (see the README roadmap): the interactive analysis view (#119), other sports (#120), weather enrichment (#121), and multi-user mode with Google/Microsoft login (#123). **#119 and #148 (the interactive analysis view, both directions) are built and merged**, as are **#121 (weather)** and **#120 (other activities, list and detail — see also #161)**. **#123 is grilled and is now an umbrella**, cut into #156 (scope every read to a rider) → #157 (sign in with Google/Microsoft, **built**) → #158 (per-rider Polar linking, and a log that starts empty, **built**) → #159 (a rider tends their own log, and can leave), in that dependency order. **The MapLibre swap (#122) was grilled and closed** — no capability wants it, and MapLibre would push the initial bundle (770 kB raw / 234 kB gzipped, error budget 1 MB) into needing a lazy-loading redesign first. So **#119 is built on Leaflet**; the old advice to swap engines before it no longer applies. Containerization and Blob Storage stay deferred too — they'd disturb the working zero-cost hosting for little visible gain.
- Phase 2 delivered: statistics records and charts, the calendar view, ride comparison, rest markers, dark mode, Hungarian translation, locale-sensitive number/date/duration formatting (#104), two-year comparison on the Trends charts (#110), the Rides coverage backdrop (#113), the four-channel ride graph with speed (#114), the switcher sizing and mobile menu fix (#124), the map refit fix (#125), the streak and monthly records (#126), the same-month-last-year dashboard tiles (#129), the top-speed and biggest-climb records (#131), the monthly time chart (#132), moving time as the canonical ride duration (#133), rest-stop detection that actually fires (#137), and the temperature and top-speed corrections (#138).
- **The weather backfill is done:** all 131 stored rides carry weather, fetched through the admin **Fetch weather** button in batches of 25 without hitting a rate limit. That answers the one number #121 had to guess at — and makes it moot: with the archive full, the daily sync finds nothing to top up and spends no calls, and steady state is at most a ride a day. Raise the batch only if a future bulk import needs backfilling again.
- **Sign-in needs registering before it works in production.** `ExternalSignIn:RedirectUriTemplate` (e.g. `https://<api>/auth/{provider}/callback`) plus a client id and secret per provider, registered in the Google and Microsoft consoles with that exact redirect. An unconfigured provider answers 404 to `/auth/{provider}/authorize`, so the login page's link is dead until both are set — it does not ask the API which providers are configured.
- **No other outstanding manual actions.** The admin **"Reprocess all"** has been run in production repeatedly through the #138 work and once more after it settled, so stored rides carry the corrected temperature summaries, top speeds and smoothed speed series. Every ride lost to a failed sync — including the Polar exercise `498528704` from 2026-07-23 — has been imported by hand via Polar Flow export → admin Import.
- Two things a reprocess used to be unable to fix, now that it can: it rewrites the **temperature summary** from the stored FIT (it only re-merged the per-point series before, so a bad value was uncorrectable by any means the owner had), and it rebuilds the **stored series**, so the speed graph changes with it.
- Test suite: **283 backend** (unit + endpoint tests in `RideLog.UnitTests`) and **305 frontend** (Vitest, 41 files).
- Working agreement with the owner: develop on `develop`, TDD via `/tdd #N`, and **only push and open a PR (develop → main) when the owner says so** — they merge it themselves.
