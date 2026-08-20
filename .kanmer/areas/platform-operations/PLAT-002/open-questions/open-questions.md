# Open questions — PLAT-002

## How broad should this cleanup be?

- [x] **Complete consolidation selected by the user on 2026-08-20.** Move every
  Web staff-actor lookup to one shared owner and remove every duplicate
  application operation-key generator. Keep the public anonymous upload page
  outside the staff-page inheritance tree; it reuses only key generation.

The narrower two-base/six-page alternative was rejected because it would leave
duplicates and could not satisfy the ticket's one-root verification.

## Parked (explicitly deferred)

None.
