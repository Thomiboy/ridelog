# A ride stays cycling, and other sports sit beside it rather than above it

Every activity the sources deliver is stored, whatever its sport, and the UI has always hidden the
non-cycling ones behind an exclusion list. Surfacing them forced a vocabulary question, because the
glossary already contradicted itself under the pressure: a **Ride** was defined as "one recorded
cycling activity", while **Sport** was "what kind of activity a *ride* records". A run therefore had
a sport but could not be a ride — a strain invisible only because runs were never shown.

We kept **Ride** meaning cycling, and added **Other activity** as its sibling: a recorded activity
whose sport is not cycling. There is deliberately **no term covering both**. The two appear as
separate lists under separate menu entries, and statistics, records and the dashboard remain about
rides alone.

## Considered options

**Widening "Ride" to mean any recorded activity** was the cheapest — nothing would be renamed and
cycling would become one value of Sport. It was rejected because the name would then lie: a swim is
not a ride. This codebase has been bitten by names that promise more than they hold, most recently a
parse result carrying a `MaximumSpeedKmh` that three separate call sites stored as a ride's top
speed; the fix there was renaming so the mistake became unavailable (see
[ADR 0003](0003-top-speed-prefers-the-device-summary.md)).

**Introducing "Activity" as an umbrella, with Ride narrowing to a cycling Activity** is the
textbook-correct model, and it is what the sources themselves use — Polar calls them exercises. It
was rejected on cost against benefit: `Ride` appears across 158 files, eight `/rides` endpoints and
roughly sixty translation keys per language, and this is a road-cycling log by name and intent
surfacing a secondary set of records. Renaming its central concept to accommodate them is
disproportionate.

**One combined list with a sport filter** was rejected as the interface of the umbrella model we had
just declined. A single list implies a single concept; there isn't one.

## Consequences

**The stored entity keeps the name `Ride` while holding every sport.** That is a known misnomer,
recorded here and in `CLAUDE.md` rather than hidden. It is tolerable where the earlier naming defect
was not: `Ride` holding a run misleads a reader momentarily, whereas `MaximumSpeedKmh` caused three
call sites to store a wrong number. The queries filter on sport visibly, at every call site.

**Sport becomes a category derived at read time**, mapped from the raw string by a pure function
rather than stored. Sources name the same sport differently — `ROAD_BIKING`, `Biking`, `cycling` —
and a recording whose name we don't recognise counts as cycling, preserving the existing rule that
untagged historical imports are rides. Deriving rather than storing means a wrong mapping is fixed by
shipping code, not by reprocessing data.

**Five of the six filtered query handlers are untouched.** Only the list gains an inverted view.
Statistics stay cycling-only because records are defined over rides, and because comparing a walk's
average speed against a ride's is meaningless.

**Adding a sport later is a category and a translation, not a concept.** Should the non-cycling side
ever grow enough to want its own statistics, that is the moment to revisit the umbrella — and this
decision is what the reversal would have to argue against.
