# Open questions — TICK-058

- [x] Retire API-02; API-01 returns a durable receipt and API-03 owns only terminal Case/PO resolution. — Operator decision, 2026-08-21.
- [x] Use the stable Pegasus Principal as the isolation boundary and retain ADR-0004. — Existing authority.
- [x] Reuse grouped durable intake, SQL outbox, Storage Queue, Worker, custody store, Container App, managed identity, and telemetry. — Code/live Azure verification.
- [x] Return no files, reports, source material, or outbound delivery. — Operator clarification and contract separation.
- [x] Do not add APIM/Front Door/Service Bus/another Function/store without measured need. — Simplicity and verified topology.

## Parked (explicitly deferred)

- [ ] Select exact route, credential presentation, request media/parts, idempotency field/header, response schema/statuses, and safe error codes. Deferred until FRD-09's public contract is deliberately approved; prior multipart, HTTP Basic, and HTTP 202 text are suggestions, not decisions.
- [ ] Name the first provider, public hostname/custom domain, request/throttle values, capacity target, and rollout date. Deferred to activation and exact-target approval.
- [ ] Support multiple simultaneous credentials or APIM gateway policy. Deferred until concrete callers/traffic justify them.
