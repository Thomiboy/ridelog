# A ride's top speed is the device's own, unless its track cannot support it

Supersedes [ADR 0002](0002-top-speed-comes-from-the-track.md), which had the track decide outright.

The ride's top speed is the maximum the source file summarises — FIT `session.MaxSpeed`, TCX lap
`MaximumSpeed` — and the track we derive from GPS positions is allowed only to **veto** it, when the
summary sits more than half again above what the filtered track supports. Either source alone stands
when the other is missing: a GPX has no summary, and a ride with no usable track has nothing to check
one against.

## Why this reverses ADR 0002

ADR 0002 was decided from a single reported symptom — one ride claiming 85 km/h — and generalised
from it. Five real exported rides, now committed as fixtures, say that generalisation was backwards:

| ride | average | device summary | GPS-derived maximum |
| --- | --- | --- | --- |
| 2024-05-05 | 20.6 km/h | **33.2** | 166.8 |
| 2024-05-10 | 22.3 km/h | **30.0** | 167.4 |
| 2024-05-28 | 23.7 km/h | **31.5** | 421.6 |
| 2024-08-14 | ~21 km/h | **37.5** | 770.3 |
| 2025-05-31 | 26.0 km/h | 85.0 | 129.4 |

Four of the five devices summarised a believable maximum; the derived figure was wrong on four of the
five. The one dishonest summary — 85 km/h — is the ride the original decision was made from, and it
happens to be exactly what that recording's first seconds of GPS warm-up read.

Filtering the track helps but does not rescue it. This device produces hundreds of spurious readings
per ride, not a handful of outliers: a rolling-window peak still reports 50-90 km/h at any window
width, and the ride's own 99th percentile still reports 30-69. The filter described in ADR 0002 left
these rides at 60, 69 and 121 km/h. A wheel sensor measures speed directly; we are reconstructing it
from positions that move by twenty metres a second under a settling fix.

The veto earns its place on the same evidence: the honest summaries all sit within a few per cent of
their filtered track, and the dishonest one at nearly twice it, so the threshold sits in a wide gap
rather than being tuned to a boundary.

## Considered options

**Trusting the device outright** is what four of the five rides would want, and it is simpler. It was
rejected because the fifth ride is the one the owner actually complained about: it would restore the
85 km/h that started all of this.

**Keeping ADR 0002 and improving the filter** was tried before this was written. Three separate
measures — a rolling window, a percentile of the ride's own distribution, and corroboration by
neighbouring samples — were run against the fixtures, and none separates this device's noise from
real riding. The noise is not a tail; it is a large population of readings.

## Consequences

RideLog's top speed now usually matches Polar Flow and the Bryton app, where before it deliberately
diverged. That is a reversal of ADR 0002's stated consequence, and the better outcome: the two only
disagree on a ride whose device is demonstrably wrong.

The per-point filter stays exactly as it is, but its remaining job is narrower: it supplies the
figure the veto is measured against — "how fast could the rider have been going here", an upper bound
that must not be eroded.

The graph is now derived separately, and deliberately so. It answers a different question — what did
the ride look like — where per-second GPS noise is the enemy rather than the signal. It averages over
a nine-second window and drops anything above the ride's top speed. That combination is what a
point-to-point derivation cannot do: one device here holds its previous position for a sample and then
advances by two samples' worth, which reads as a standstill followed by double the real pace, and no
amount of filtering recovers the steady 35 km/h the rider was actually doing. On that ride 42% of the
plotted line was a hole or a false standstill; it is now 2%, and the zeros that remain are the stops
the rider actually made.

Existing rides need an admin **Reprocess all** to pick this up.
