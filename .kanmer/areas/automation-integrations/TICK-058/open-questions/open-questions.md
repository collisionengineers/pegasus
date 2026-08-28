# Open questions — TICK-058

- [x] Retire API-02; API-01 returns a durable receipt and API-03 owns only terminal Case/PO resolution. — Operator decision, 2026-08-21.
- [x] Use the stable Pegasus Principal as the isolation boundary and retain ADR-0004. — Existing authority.
- [x] Reuse grouped durable intake, SQL outbox, Storage Queue, Worker, custody store, Container App, managed identity, and telemetry. — Code/live Azure verification.
- [x] Return no files, reports, source material, or outbound delivery. — Operator clarification and contract separation.
- [x] Do not add APIM/Front Door/Service Bus/another Function/store without measured need. — Simplicity and verified topology.
- [x] Exact route, credential presentation, request media/parts, idempotency header, response schema/statuses and error codes. — Settled 2026-08-28 in FRD-09 § Accepted API-01 submission contract (activated by EPIC-011 D8): `POST/GET /api/provider/v1/submissions`, `Bearer pgs_…`, multipart `files` + `providerReference`, `Idempotency-Key`, 201/200/409/403/413/401/404 problem details.
- [x] How a provider submission binds to its Principal inside processing. — The `ProviderSubmissions` row is the binding; `ProcessIntake` reads it through `IProviderSubmissionBindings` for the `provider_api` channel and skips mail-route selection; a Principal without an extraction policy is NeedsSorting.

## Parked (explicitly deferred)

- [ ] Name the first provider, public hostname/custom domain, request/throttle values beyond the code default (60/min/key), capacity target, and rollout date. Deferred to activation and exact-target approval (capabilities.md boundary).
- [ ] Support multiple simultaneous credentials per Principal or APIM gateway policy. Deferred until concrete callers/traffic justify them.
- [ ] Result lookup beyond the submission's own Case/PO reference (API-03 scope). Deferred; TICK-060 owns it.
