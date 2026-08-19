# Open questions — MAIL-10

No unresolved operator question. Require deliberate Case search, target summary, reason, explicit confirmation and optimistic concurrency; preserve prior association and never mutate Case/reference identity.

## Parked (explicitly deferred)

- [x] **What live production verification is required?** — Resolved by the operator on 2026-08-19: exercise the full manual association journey in production—link an exact retained message to an exact initial Case, unlink it with a reason, then relink it to an exact replacement Case with a reason. Immediately before the writes, obtain/record exact-target approval naming the message, both Cases, and approved reasons. Capture confirmation summaries, optimistic versions, before/after state, attribution, and every append-only history entry. Abort on stale state or target mismatch; this planning decision does not authorize unspecified production writes.
