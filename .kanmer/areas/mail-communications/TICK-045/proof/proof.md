# Proof — TICK-045 (MAIL-03)

## Merge

PR #422, merge commit `00a6787f14e9540835b684d24f0f0dcdfae77548` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `00a6787f` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks.

## Production evidence

TICK-045 delivered the shared classification policy **and**, on the same PR
diff, wired `MailOperationalDestinationPolicy` into the retained-mail Core
projection as its first real caller — the MAIL-02 caller TICK-044's own
checklist records as landing on this PR rather than TICK-044's branch (see
TICK-044's checklist, "Wire `MailOperationalDestinationPolicy`..." item).
Per [[DELIV-012]] proof's signed-in production verification: `/Inbox/{id}` on
a real classified e-mail shows "Operational destination: Receiving work —
Destination policy: mail_operational_destination version 1" — the shared
classification policy computing live in production, one policy across all
supported mailboxes, exactly as this ticket specifies.

## Qualification

None — this is a direct production caller of the shipped policy, not an
inferred effect.
