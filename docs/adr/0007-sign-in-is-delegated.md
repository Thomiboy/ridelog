# Sign-in is delegated, and a verified email is who you are

Opening the app beyond its one seeded admin needs a way for riders to arrive. New riders sign in with
Google or Microsoft only. There is no password registration, and a rider is identified by the email
their provider vouches for.

## Why no passwords

**There is no email pipeline anywhere in this codebase** — no sender, no service, nothing.
(Amended by #168, below: there is now a sender, but it reaches the owner only and still cannot mail a
rider — so this reasoning stands.) Passwords
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

## Amendment (2026-08, #168): there is now a sender, to the owner only

The app can send mail — a contact form lets a visitor reach the owner. The load-bearing sentence above
is therefore no longer literally true, and this records what replaced it so the next reader is not left
to guess.

What ruled out local passwords was **mailing riders**: address verification and password reset both
send mail to a rider, at a cold address, from a brand-new sender on `azurewebsites.net` — a domain
nobody here owns, so no SPF or DKIM can be published and the mail lands in spam. A verification or
reset that *sometimes* arrives is worse than none. None of that changed.

So the sender is shaped to keep it true where it matters: `IOwnerMailSender.NotifyOwnerAsync` **takes no
recipient**. The owner's address comes from configuration, so "nothing here can mail a rider" is a fact
the signature keeps rather than a rule someone remembers. The owner is one recipient, at an address they
watch, who can whitelist a sender once — nothing like mailing a rider. Mailing anyone else is a new
method and a new decision, not a `to` argument passed on a Tuesday, and **that** change is the one that
would genuinely reopen this ADR. Until a domain is bought and its deliverability done, riders are still
reached only through the app.
