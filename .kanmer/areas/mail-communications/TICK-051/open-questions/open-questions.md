# Open questions — MAIL-09

- [x] **What evidence may automatically associate email with a Case?** — Case/PO references are internal and must not be used to match inbound mail. A VRM automatically matches only when exactly one Case in the system has that VRM. Thread evidence automatically matches when the exact retained mailbox thread already belongs to a Case; use mailbox-scoped durable thread identity and fail closed if the thread is associated with zero or multiple Cases or contradictory evidence.

## Parked (explicitly deferred)

- [x] **Is live production association required?** — Yes. Resolved by the operator on 2026-08-19: TICK-051 acceptance must include one live automatic association between an exact retained message and an exact Case. Immediately before the write, obtain/record approval naming both targets and verify the accepted evidence is either a system-wide unique VRM or a mailbox-scoped retained thread already associated with exactly one Case. Do not infer authority from this planning decision alone. Capture the resulting permanent association history and prove replay is idempotent; abort on zero, multiple, stale, or contradictory evidence.
