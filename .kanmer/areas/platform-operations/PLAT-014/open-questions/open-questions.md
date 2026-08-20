# Open questions — PLAT-014

No operator decision is required.

## Resolved technical constraints

- [x] Missing LocalDB responses that exit 0 are observed on this Windows LocalDB 2025 installation; only the explicit “doesn't exist” response is eligible to normalize to `Missing`.
- [x] A zero-exit response without a known state or the explicit missing-instance signal remains `Unknown` and must stay fail-closed.
- [x] Existing script-test conventions do not provide a reusable lifecycle test harness; planning may add the smallest focused Windows assertion script and CI invocation necessary for this regression.

## Parked (explicitly deferred)

- [Reason: outside this fix] Any change to database ownership, reference allocation, LocalDB naming, or the Linux container lifecycle.
- [Reason: owned by linked ticket] Screenshot capture and visual proof remain in [[PLAT-005]] after this lifecycle is verified.
