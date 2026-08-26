# A month is read elsewhere

A signed-in rider can ask for a written reading of one calendar month of their riding: what it was
made of, how it sits against the twelve months before it, and what the numbers invite them to try.
The prose is written by a language model at Anthropic.

Two things about that are firsts for this project, and neither should arrive quietly.

## It costs money per use

Every other decision here has bent to the zero-cost rule: App Service F1 over containers, Azure SQL
free over Blob Storage, a daily cron instead of an hourly one, Leaflet over MapLibre. This is the
first feature where using the app spends money.

The amount is not the point, and pretending otherwise would be dishonest in the other direction: a
reading is a few thousand tokens in and a few hundred out, and the whole quota is structural, so a
three-year log has a lifetime ceiling somewhere around seventy readings — a few dollars, once, and
they cannot recur. The point is that the rule is being **set aside deliberately for one feature**,
with a ceiling that can be reasoned about in advance, rather than eroded by a series of small
exceptions nobody wrote down.

Two things keep it that way. The quota is the storage key — rider, month and language identify a
reading, a closed month never changes, so it is written once and read free from then on; there is no
counter to keep and no window to reset. And the switch defaults to **off**, unlike the contact form's
(#168), which defaults to on because it costs nothing: a feature that starts spending because nobody
turned it off is the wrong default.

## A rider's figures leave the app

Nothing has left this app before that describes a rider. Polar is the rider's own account; Open-Meteo
receives a coordinate and an hour; Resend carries a message a stranger wrote on purpose. This sends
one rider's training figures to a third party.

So the boundary is a type. `MonthlyTrainingSummary` is the only argument `ITrainingAnalyst` takes,
and there is nowhere in it to put a rider id, a name, an address, a ride id, a coordinate or a route.
That is the same move as `IOwnerMailSender.NotifyOwnerAsync` taking no recipient (#168, docs/adr/0007
amendment): the guarantee is a fact about the signature rather than a rule a caller has to remember.
Tests walk what the type can reach rather than what today's builder happens to put in it.

The per-point metric series stays behind for the same reason and one more: elevation and speed point
by point is the route in all but name, and the route of a ride that starts at your front door is your
address (docs/adr/0006). What the series is *worth* — minutes per heart-rate zone, kilometres per
temperature band — travels instead, worked out here.

## The model reads; it does not compute

Every figure in a summary is one the Statistics page already computes, and the instructions forbid
working out new ones. This is docs/adr/0003's rule against two paths to one number, applied where it
bites hardest: the analysis renders directly beneath the Trends charts, and a reading that
contradicted the chart above it would be worse than no reading. Monthly totals therefore come from
one shared place, and a test fails if either reader is given its own arithmetic.

## Considered options

**A rules engine — structured findings the frontend renders through Transloco.** Genuinely tempting:
it would be translatable, verifiable, and free. Rejected because the knowledge would live in the
catalogue of findings rather than in the model, which makes it a rules engine where an expensive and
less predictable component picks which rule fires. If that is what we want, the honest version is
`if` statements and no API key.

**Per-ride analysis instead of per-month.** Rejected as the more obvious and the less useful of the
two: the ride page already shows distance, duration, speed, heart rate, zones, elevation, calories,
temperature, weather, headwind and rest stops, so a reading of one ride mostly restates the cards the
rider is looking at. A month can say things no card on any page answers.

**A rolling window ("the last eight weeks") or a free date range.** Rejected on the quota: both are a
different set of rides every day, so the cache never hits and two readings a day apart cost twice for
no difference. A calendar month is also the grain the charts are already drawn on.

**Bring-your-own API key.** The most defensible on cost and dead on arrival: cyclists do not hold
Anthropic API keys, and storing one per rider is more work than the feature.

## Consequences

**Approval is the spending gate.** Every approved rider may write readings on the app's key, and the
exposure is bounded by an act the owner already performs one rider at a time (#165). Making this
admin-only was rejected as a step back into the pattern #156–#159 spent four issues undoing: the
admin role guards `/import` for a *resource* reason, not an ownership one.

**Prose is not translatable, only rewritable.** The language is part of what identifies a stored
reading, and switching the UI language shows the stored one with a note rather than spending a second
generation. Nothing in the app generates as a side effect of anything; a reading is written only when
somebody presses the button.

**A failed call leaves nothing, and can be retried.** Storing happens after the call — the opposite
of `/contact`, which stores first because the visitor's message has value before the send. Here the
call's result is the whole value. The accepted cost is that a persistently failing call is repeatable
spend; the timeout bounds each attempt and the kill switch is the backstop.

**It describes and suggests; it does not prescribe.** There is no power data, no rest or recovery
data and no injury history here, and the maximum heart rate the zones rest on is a number the rider
typed in themselves. Anything shaped like a training plan would be invented, so four limits are part
of the request rather than part of its tone, and the section carries a note saying where the
sentences came from — the same habit as the ride page's temperature and weather notes.

**The SDK, not a hand-rolled client.** The one deliberate break from how this codebase reaches third
parties. Polar, Open-Meteo and Resend are hand-rolled because they aim at stationary targets; the
Messages API is not stationary, and a refusal arrives as a stop reason rather than an error status —
read the content without checking it and you store an empty reading that looks like a model with
little to say.
