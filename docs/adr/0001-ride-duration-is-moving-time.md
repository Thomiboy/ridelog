# A ride's duration is moving time, taken from the device where possible

RideLog ingests rides from GPX, TCX and FIT, and until now stored whatever duration each parser
happened to produce: FIT contributed the device's own timer (moving time), while GPX and TCX
contributed first-point-to-last-point elapsed time. Durations were therefore not comparable between
rides, and totalling them added two different measures together. We have settled on **moving time**
as what "how long was the ride" means, preferring the source's own timer (TCX lap `TotalTimeSeconds`,
FIT `TotalTimerTime`) and falling back to elapsed time only where the source records no timer.

## Considered options

**Elapsed time everywhere** was the simpler alternative: every source supplies it directly, and no
fallback logic is needed. We rejected it because it discards the more accurate reading the device
already made, and because it would contradict how the same parsers already compute average speed —
`TcxActivityParser` deliberately derives speed from `TotalTimeSeconds` rather than elapsed time, so
storing elapsed duration alongside it made the two disagree about the same ride.

## Consequences

Rides imported from GPX or TCX before this decision keep their elapsed-time duration until an admin
**Reprocess all** rebuilds them from the stored raw files, so the history is briefly mixed. Moving
time remains a per-source approximation rather than a single definition: a device that does not
auto-pause reports its timer as elapsed time anyway, and GPX has no timer at all, so the fallback is
part of the definition rather than a defect in it.
