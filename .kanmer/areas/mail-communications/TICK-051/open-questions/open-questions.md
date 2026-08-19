# Open questions — MAIL-09

- [x] **What evidence may automatically associate email with a Case?** — Case/PO references are internal and must not be used to match inbound mail. A VRM automatically matches only when exactly one Case in the system has that VRM. Thread evidence automatically matches when the exact retained mailbox thread already belongs to a Case; use mailbox-scoped durable thread identity and fail closed if the thread is associated with zero or multiple Cases or contradictory evidence.

## Parked (explicitly deferred)

- [ ] Real Outlook/Graph/cloud activation and live verification — requires explicit approval for exact targets and operations.
