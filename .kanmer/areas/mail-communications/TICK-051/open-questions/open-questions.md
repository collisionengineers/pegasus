# Open questions — MAIL-09

- [x] **What evidence may automatically associate email with a Case?** — Case/PO references are internal and must not be used to match inbound mail. A VRM automatically matches only when exactly one Case in the system has that VRM. Thread evidence automatically matches when the exact retained mailbox thread already belongs to a Case; use mailbox-scoped durable thread identity and fail closed if the thread is associated with zero or multiple Cases or contradictory evidence.

## Parked (explicitly deferred)

- [x] **Is live production association required?** — Yes. Resolved by the operator on 2026-08-19: TICK-051 acceptance must include one live automatic association between an exact retained message and an exact Case. Immediately before the write, obtain/record approval naming both targets and verify the accepted evidence is either a system-wide unique VRM or a mailbox-scoped retained thread already associated with exactly one Case. Do not infer authority from this planning decision alone. Capture the resulting permanent association history and prove replay is idempotent; abort on zero, multiple, stale, or contradictory evidence.

# Research refresh — 2026-08-20

No new unresolved operator question was found. The accepted rule remains:

- normalized VRM only when exactly one Case system-wide carries it;
- or exact durable conversation identity when the retained thread in that mailbox resolves to exactly one Case;
- both evidence types must agree when both exist;
- zero, multiple, stale or contradictory evidence abstains;
- inbound Case/PO is not a MAIL-09 automatic key.

Planning may choose only the smallest atomic revalidation mechanism inside the existing association transaction; it may not weaken these rules.

## Parked (explicitly deferred)

- [x] **Is live production association required and already authorized?** — Required for acceptance, but not pre-authorized. Immediately before the production DB write, obtain exact-target approval naming the retained message, Case and qualifying evidence; abort on ambiguity, staleness, contradiction or target mismatch. Capture before/after, permanent history, actor and replay. No Graph, Outlook or mailbox mutation is permitted by this decision.
