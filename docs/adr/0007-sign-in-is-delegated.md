# Sign-in is delegated, and a verified email is who you are

Opening the app beyond its one seeded admin needs a way for riders to arrive. New riders sign in with
Google or Microsoft only. There is no password registration, and a rider is identified by the email
their provider vouches for.

## Why no passwords

**There is no email pipeline anywhere in this codebase** — no sender, no service, nothing. Passwords
without one means no address verification and, worse, no password reset: an account whose password is
forgotten is an account that is gone, along with every ride in it. Building that pipeline is its own
project, and it means finding another service to keep inside the zero-cost hosting rule.

Google and Microsoft supply an email their side has already verified, which is exactly what Identity
is configured to want. The seeded admin keeps its password, so there is still a way in if a provider
is unreachable, and nothing about the existing sign-in breaks.

## Why the same email is the same rider

Identity is configured with `RequireUniqueEmail = true`. That is not a preference we are working
around — it decides the question. A rider who signs in with Google today and Microsoft in six months
presents the same address both times, and Identity cannot create a second account for it. Either the
second provider attaches to the existing rider, or the second sign-in simply fails with nothing the
rider can do about it.

So it attaches — **but only when the provider states the email is verified.** An unverified address
from any provider is refused, because accepting one would let anyone who can get a token bearing
someone else's address walk into their log.

Allowing duplicate emails was the alternative, and it was rejected for being worse than the problem:
the rider's log would silently split in two, and nothing on screen would explain where half of it
went.

## Why the token never rides in a URL

The standard external-login flow keeps intermediate state in a cookie. This app is deliberately
cookie-free — the frontend and the API are separate origins and it uses JWT bearer throughout — so
the provider's callback has to hand something back across a redirect.

It hands back a short-lived, single-use code, which the frontend exchanges for a token. Putting the
token in the URL instead would have been less work: in the fragment it at least stays out of server
logs and referrer headers, but it stays in browser history either way, and on a shared machine that
is a real way to lose an account. The existing `/polar/callback` already does this shape of redirect
dance, so the ground is known.

## Consequences

**Riders arrive without a password, which means there is nothing to reset and nothing to leak.** It
also means the app is only as reachable as Google and Microsoft are — accepted, given the alternative
is an email pipeline nobody has.

**Adding local passwords later is not a small change.** It needs the pipeline, and it needs a story
for accounts that already exist without one. That is the reversal cost, and it is why this is written
down rather than assumed.
