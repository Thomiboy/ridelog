# Weather is reported, not measured, and never fills in for a measurement

A ride can carry a temperature reading of its own, merged from a Bryton FIT and summarised on the
ride as an average, minimum and maximum. Most rides have no such file, so most rides have no
temperature, and the Statistics temperature bands are thin as a result.

Adding a historical weather lookup makes an obvious offer: fill the gap. We declined it. Weather is
stored as its own thing, and the ride's measured temperature keeps its field, its meaning and its
place in the statistics.

## Why the offer was declined

The two numbers are not the same quantity. A sensor on the handlebars reads the air the rider is
actually moving through — warmed by sun on a dark frame, cooled by a descent, raised by a city.
A weather service reports the area, from a station or a reanalysis grid that may be kilometres away
and hundreds of metres lower.

Merging them would make `AverageTemperatureCelsius` mean one thing for some rides and another for
the rest, with nothing on the ride to say which. That is the defect
[ADR 0001](0001-ride-duration-is-moving-time.md) was written to remove from ride duration, and
the temperature bands, the coldest and warmest ride records and the monthly averages would all
inherit it — each one silently comparing measurements against estimates.

The cost is real and was accepted knowingly: **the temperature statistics stay as sparse as they are
today.** Denser numbers that mean two things are worth less than sparse numbers that mean one.

The glossary already implied the answer. A **Channel** is "one measurable quantity along a ride" that
"a ride has only where its source recorded it". Weather is not recorded by a ride's source, so it was
never a channel; making it one would have required rewriting the definition first.

## Consequences

**Weather is stored hourly, not as one reading per ride.** A three-hour ride can start calm and
finish in a gale, and a single average hides exactly the ride worth explaining. It is drawn on the
ride graph as its own layer rather than as a selectable channel, so the channel picker keeps meaning
"what this ride measured" — and so that a four-point hourly trace is not mistaken for the same kind
of thing as a five-hundred-point one.

**Wind is stored raw and headwind is derived at read time.** The stored fact is what the service
reported: speed and direction. How much of it opposed the rider is computed from the route's bearing
when read, so a wrong calculation is corrected by shipping code rather than by re-querying every
ride. Note that services report wind at 10 m; a rider in the saddle feels less, and the figure should
never be presented as "the wind you felt".

**The lookup runs after the ride is committed, in its own unit of work.** The import transaction
commits even when an exercise fails, so a lost ride is not re-served by Polar and has to be recovered
by hand. Putting a third-party call inside that transaction would turn any weather outage into that
same recovery. A ride with no weather is a ride that can be topped up later; a ride that never
imported is not.

**Each attempt's outcome is recorded** — fetched, unavailable, or failed. Without it the daily
top-up would retry rides that can never succeed, such as imports predating the service's coverage,
every morning and against the quota. The public UI shows weather or nothing; counts belong on the
admin page beside the sync figures.
