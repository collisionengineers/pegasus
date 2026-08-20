# Open questions — PLAT-002

## How broad should this cleanup be?

- [x] **Complete consolidation selected by the user on 2026-08-20.** Move every
  Web staff-actor lookup to one shared owner and remove every duplicate
  application operation-key generator. Keep anonymous Uploads/Request outside
  staff inheritance while it reuses operation-key generation. Keep
  Upload.ExternalReceiptToken generation local because it is a separate intake
  replay/receipt identity, not an operation key.

The narrower two-base/six-page alternative was rejected because it would leave
actor and operation-key duplicates and fail the ticket's one-root acceptance.

## Parked (explicitly deferred)

None.
