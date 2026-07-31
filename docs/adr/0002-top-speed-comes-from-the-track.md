# A ride's top speed comes from its track, not the device's summary

> **Superseded by [ADR 0003](0003-top-speed-prefers-the-device-summary.md).** This decision was made
> from one reported symptom; five real rides later showed the device summary is right far more often
> than the track. The per-point filtering described below is still in force — it decides the graph's
> speed channel, and supplies the figure ADR 0003's veto is measured against.

Every source hands us a maximum speed of its own — FIT `session.MaxSpeed`, TCX lap `MaximumSpeed`,
and Polar by way of the TCX it serves — and we stored it unexamined. A single GPS jump lands in that
summary, so rides reported speeds no bicycle reaches (85 km/h), and once the top-speed record (#131)
started ranking rides by that field, one glitched ride owned the record permanently. We now derive
the maximum from the ride's own track: each point's speed resolved as it already is for the graph
(the device's per-point reading where the file has one, otherwise from position and time), with
readings rejected when they require a rise of more than 15 km/h per second — well beyond any real
sprint start, and far below what a position jump produces. The device summary survives only as a
fallback for a ride whose track offers no usable speed at all.

This runs opposite to [ADR 0001](0001-ride-duration-is-moving-time.md), where the device's own timer
*was* adopted as the truth. The two are consistent in principle rather than in direction: we prefer
whichever reading the source is actually in a position to know. A device times its own moving
intervals better than we can reconstruct them, but it summarises a maximum from the same noisy
samples we hold — and unlike us, it never revisits that number once written.

Two exported rides from the same device, kept as fixtures, are why this is not negotiable. One
summarised a believable 37.5 km/h. The other claims 85 km/h on a ride averaging 26 — and 85 is
precisely what its own first few seconds of GPS warm-up read, before the fix had settled. A device
summary is sometimes right, which is worse than being reliably wrong: it cannot serve as a check on
anything, only as a last resort when the track offers nothing at all.

## Considered options

**An absolute ceiling** — reject anything above, say, 80 km/h — was the smallest change, and was
rejected as arbitrary: it either clips a genuinely fast descent or lets smaller glitches through,
and the affected ride carried a second, smaller spike that such a bound would have passed.

**Redefining the maximum as a sustained speed** (the peak of a rolling few-second average) was
attractive because it needs no judgement about which readings are "wrong" — an isolated spike simply
disappears. We rejected it because it also shaves genuine short peaks, and a record should be the
real top speed rather than a smoothed one. It is also meaningless on a sparse track.

**Cross-checking the summary against the track**, keeping the device's number unless the track
disagrees, was rejected as the worst of both: it keeps a value we have decided we cannot trust, and
still needs the whole track-derived calculation to decide when to distrust it.

## Consequences

RideLog deliberately shows a different top speed from Polar Flow and the Bryton app. That is the
point of the decision, but it means the numbers are not expected to match, and a bug report of the
form "the app says 85 and RideLog says 58" is this decision working.

Rides imported before this keep their old maximum until an admin **Reprocess all** rebuilds them
from the stored raw files. The raw files are kept, so the threshold stays tunable afterwards.

"The track" means the route the ride actually stores, which is not always the file the scalar metrics
came from: Polar sync and reprocessing prefer the TCX for its heart rate but fall back to the GPX
route when the TCX has no positioned points. Deriving the maximum inside the parser was therefore not
enough — a TCX with an empty track handed over its lap summary while the graph was built from the
GPX.

Parsers no longer answer the question at all. `ParsedActivity` carries `DeviceMaximumSpeedKmh`, the
file's own summary verbatim, and every site that builds a ride derives the top speed from the route
it is about to store, falling back to that summary only when the route yields no speed. The naming
is the enforcement: there is no longer a `MaximumSpeedKmh` on a parse result that a caller could
store by mistake, which is how this went wrong three times running.

GPX rides gain a maximum for the first time — GPX records no speed at all, so they previously had
none. Their speed is derived from position and time, which is noisier than a wheel sensor; the same
filter is what makes that acceptable.

A track's opening reading has nothing before it, so the rise bound cannot judge it. We first
accepted that as a limit; the rides imported afterwards opened on GPS fixes reading 100–421 km/h, so
it isn't one we can live with. The opening reading is now held provisionally and judged by what
comes next: a drop of more than 30 km/h per second — beyond the limit of tyre grip, never mind
brakes — is not deceleration, and condemns the reading it fell from. The bound is deliberately far
looser than the 15 km/h rise bound, because braking genuinely is more abrupt than accelerating; it
only rules out drops no brake produces. A ride that truly opens at speed and then brakes hard keeps
its reading.

Both bounds scale with the interval between readings, but only up to two seconds' worth. Acceleration
decays within a couple of seconds as drag takes over, so a rider cannot bank a bigger jump by being
sampled less often — and without the cap the rule has no teeth where tracks are sparse. Polar's smart
recording samples about every ten seconds, which under a purely per-second bound licensed a 150 km/h
rise: a 300 m fix jump read as 108 km/h and passed unchallenged.

Only a reading we accept may settle an unproven opening one. A rejected reading is itself bogus and
says nothing, so the opening stays pending until something trustworthy arrives. A real ride caught
this: its opening interval read 190 km/h and the one after it a wilder 770 km/h, and treating the
second as a verdict on the first left the 190 standing for good.

Two further details make the opening check actually reach the glitch. Derived speed no longer **borrows** for the
first point: it used to take the second point's reading so the chart wouldn't dip at the start, but
that made the opening pair identical by construction, and a glitched first interval then produced
two matching bogus readings that no rate-of-change rule can separate. And a reading that replaces a
condemned one inherits the provisional status, so a fix that drifts back over a second sample loses
both halves rather than keeping the second as the new maximum.
