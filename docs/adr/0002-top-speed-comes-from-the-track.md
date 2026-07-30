# A ride's top speed comes from its track, not the device's summary

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

GPX rides gain a maximum for the first time — GPX records no speed at all, so they previously had
none. Their speed is derived from position and time, which is noisier than a wheel sensor; the same
filter is what makes that acceptable.

The filter cannot judge the very first reading of a track, because there is nothing before it to
measure against. A ride whose opening sample is already a glitch keeps it. This is inherent to a
rule expressed as a rate of change, and is accepted rather than worked around.
