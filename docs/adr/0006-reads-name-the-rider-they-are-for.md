# Reads name the rider they are for

Every stored entity already carries a `UserId`, the duplicate guard is scoped by it, and
`Ride.Overlaps` compares it first. That made the write side multi-user-ready some time ago.

The read side was not. All six query handlers — rides, ride, dashboard, statistics, longest rides,
route coverage — read the whole table, and `GetRidesQuery` and `GetStatisticsQuery` carried no rider
at all. With one rider that is invisible. With two it means every public page mixes both riders'
rides together, and the statistics compare heart-rate zones across people using whichever max heart
rate each of them configured.

So every read now names the rider it is for. `GetRidesQuery` and its siblings carry a rider, and the
endpoint decides who that is: the signed-in rider for their own log, and the configured
[public log](../../CONTEXT.md) rider for anyone who is not signed in.

## Considered options

**An ambient "current rider" resolved inside the handlers** would have left the queries and the
endpoints untouched. It was rejected for the reason the defect existed in the first place: nothing
on the query said whose data it wanted, so nothing made its absence noticeable. A handler written
next year cannot forget a parameter it has to supply; it can very easily forget to consult a service.

**Putting the rider in the route** (`/riders/{id}/rides`) reads well and would suit public profile
pages later. It was rejected as premature: it rewrites every endpoint and every frontend call in
service of per-rider public logs, which we deliberately did not adopt — there is one public log.

## Consequences

**Nothing visible changes yet.** There is one rider, so every page returns exactly what it returned
before. That is the point of doing this first: it is a prerequisite for registration, and doing it
afterwards would mean the first stranger to sign up could read everyone's rides and routes — which,
for a ride that starts at your front door, is your address.

**Which log is public is a setting, not a role.** Today the admin and the public log are the same
person, but they answer different questions: one is what a rider may do, the other is whose rides the
world can see. Deriving the public log from the admin flag would mean the showcase silently follows a
permission.

**Multi-user arrives as a sequence, not a change.** This is the first slice; registration and
sign-in, per-rider Polar linking, and separating admin from ownership each follow behind it. The
umbrella is #123.
