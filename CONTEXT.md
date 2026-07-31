# RideLog

A personal road-cycling log: rides arrive automatically from Polar, optionally enriched from a
manually uploaded Bryton file, and are presented as statistics, charts and route maps.

## Language

### Activities

**Ride**:
One recorded cycling activity, from a single source event. Rides from different sources that cover
the same time span are the same ride, merged rather than stored twice.
_Avoid_: Activity, session, workout, exercise

**Other activity**:
One recorded activity whose sport is not cycling — a run, a walk, a hike, a swim. It arrives from
the same sources as a ride and is kept, but it is not a ride and nothing is a term for both.
_Avoid_: Workout (excludes walking), exercise, training

**Sport**:
What kind of activity was recorded, as the source reported it. Sources name the same sport
differently, so what the app reasons about is the category the raw name falls into; a name it does
not recognise counts as cycling, because an untagged recording here is a ride.
_Avoid_: Type, discipline

### Time

**Moving time**:
Time actually spent riding, excluding stops. This is what "how long was the ride" means throughout
RideLog — the duration record, and any total of time ridden. Where a source records its own timer,
that reading is authoritative; otherwise moving time falls back to elapsed time.
_Avoid_: Duration (ambiguous), riding time, timer time, active time

**Elapsed time**:
Wall-clock time from the first recorded point to the last, including every stop. Used as the x-axis
option on the ride graph, and as the fallback when a source records no moving time.
_Avoid_: Total time, wall time

**Rest stop**:
A pause within a ride, inferred from the ride's own data rather than reported by the source. Rest
stops are what separate moving time from elapsed time.
_Avoid_: Break, pause, stop

### Measurement

**Metric series**:
A ride's per-point measurements over its course — the sampled channels the ride graph draws.
Downsampled, so it describes the shape of a ride rather than every recorded instant.
_Avoid_: Samples, track, telemetry, points

**Channel**:
One measurable quantity along a ride: elevation, heart rate, temperature or speed. A ride has a
channel only where its source recorded it.
_Avoid_: Metric, series, field

**Route**:
The path a ride took, as positions. Distinct from the metric series, which carries no positions.
_Avoid_: Track, path, polyline

**Top speed**:
The fastest a ride went, as its recording device measured it — unless the ride's own track cannot
support that figure, in which case the track's.
_Avoid_: Max speed, peak speed

### Aggregates

**Record**:
A personal best across all rides — the single ride, streak or month that leads a given measure.
_Avoid_: Highlight, achievement, PB

**Streak**:
Consecutive calendar days each carrying at least one ride. Several rides on one day count once.
_Avoid_: Run, series
