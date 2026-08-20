# Proof — TICK-044 (MAIL-02)

## Merge

PR #411, merge commit `dc77c29d6d987875c4863aacc9975bf0a6334308` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `dc77c29d` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks.

## Production evidence — provenance note

TICK-044 built the `MailOperationalDestinationPolicy` Core contract, but its
own checklist records that the real production caller — wiring the policy
into the retained-mail projection and rendering it on `/Inbox/{id}` — landed
on **[[TICK-045]]'s PR #422** (merge `00a6787f14e9540835b684d24f0f0dcdfae77548`,
also release 12), not on this ticket's own branch/diff. This is disclosed in
TICK-044's checklist verbatim: "Recorded here because the work satisfies this
item but landed on TICK-045's diff, not this ticket's branch."

Per [[DELIV-012]] proof's signed-in production verification: `/Inbox/{id}` on
a real classified e-mail shows "Operational destination: Receiving work —
Destination policy: mail_operational_destination version 1" — the formerly
dark MAIL-02 policy computing live in production.

## Qualification

The policy itself (this ticket's own diff) is proven only via the caller
that shipped on TICK-045's PR. This is the same underlying release-12 Web
image, so the production evidence is real, but the caller code is not on
TICK-044's own commits — recorded honestly rather than claimed as this
ticket's own end-to-end proof. Separately: the destination enum member this
ticket shipped as `MailOperationalDestination.NeedsSorting` was later
renamed to `Unidentified` by [[INTK-007]] (still release 12) — a vocabulary
change only, not a behaviour change.
