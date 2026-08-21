# Open questions — TICK-058

- [x] Retire API-02 as a standalone status feature; API-01 returns only the durable receipt and API-03 owns terminal retrieval. — Operator decision, 2026-08-21.
- [x] Use the stable Pegasus Principal as the isolation boundary. — FRD-09 and ADR-0004.
- [x] Use a bounded `multipart/form-data` request with one JSON instruction part plus file parts, translated to the existing grouped intake contract. — Planning default; avoids base64 expansion and duplicate policy.
- [x] Require an `Idempotency-Key` header scoped to the authenticated credential and return the same receipt on exact replay; reject conflicting reuse. — Existing intake semantics.
- [x] Return HTTP 202 with an opaque receipt identifier after durable acceptance; do not wait for processing. — Operator decision and durable architecture.

## Parked (explicitly deferred)

- [ ] Name the first live provider, exact public hostname, request limits below repository maxima, throttling values, and rollout date. Deferred because activation and external writes require separate exact-target approval.
- [ ] Decide whether a future provider needs a second concurrent credential. Deferred until a second concrete caller exists; v1 uses one credential per Principal.
