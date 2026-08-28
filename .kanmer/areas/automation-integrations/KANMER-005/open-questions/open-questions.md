# Open questions — KANMER-005

- [x] **Does a successful save end the edit lease?** — Resolved by the
  operator on 2026-08-28: preserve the current behavior and [[CASE-024]]
  contract. A successful mutation consumes the lease. After a rejected
  competitor, the holder may instead release without saving; a separate
  `edit_end` after a successful save is expected to be refused.

## Parked (explicitly deferred)

None.
