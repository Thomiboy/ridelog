# RideLog

A personal road-cycling log: rides arrive automatically from Polar, optionally enriched from a
manually uploaded Bryton file, and are presented as statistics, charts and route maps.

## Language

### People

**Rider**:
Whoever rode. Every ride belongs to exactly one, and a rider signs in as themselves — the account
and the person are one thing here, since nobody keeps a log on somebody else's behalf.
_Avoid_: User, athlete, owner (that's a role, below)

**Public log**:
The one rider's rides a visitor can see without signing in. A rider's log is private otherwise, so
this names a single rider deliberately rather than describing a setting each rider has.
_Avoid_: Profile, public account, showcase

**Admin**:
A rider who may also run maintenance over stored rides — reprocessing and deleting. What a rider is
allowed to do, which is a different question from whose log the world can see.
_Avoid_: Owner, superuser

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

### Getting rides in

**Polar link**:
The connection between a rider and their Polar account, which is what makes rides arrive on their
own. It has a moment: Polar only delivers sessions recorded *after* a rider links, so nothing from
before a link ever arrives automatically, and a rider's automatic history starts the day they made
one.
_Avoid_: Connection, integration, pairing

**Sync**:
Collecting whatever a rider's Polar link has delivered since the last time. Repeatable and
unattended: a ride already stored is recognised and left alone rather than stored twice.
_Avoid_: Fetch, pull, refresh

**Import**:
Adding rides from files rather than from a link — the only way anything from before a Polar link can
be in a log at all. Distinct from a sync in what it costs someone: a sync happens to a rider, an
import is something they do.
_Avoid_: Upload (that's the file), bulk load, backfill

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

**Weather**:
The conditions reported for a ride's time and place by a third party. Reported, not measured: it
describes the area a ride passed through, where a channel describes the ride itself. A ride's own
temperature reading and the weather's temperature are therefore different quantities, never one.
_Avoid_: Conditions, climate

**Headwind**:
How much of the weather's wind opposed the direction actually being ridden. Derived by comparing the
two, so it exists for any ride that has both a route and weather, and means nothing on its own for a
loop — a lap returns whatever it borrowed.
_Avoid_: Wind (that's the weather's), wind resistance, drag

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
