# Plan — TICK-058: Principal-scoped provider submission API

## Approach

After TICK-061 supplies credential authentication, add one thin provider POST endpoint that translates a bounded multipart request into the existing grouped durable-intake command. Return after durable acceptance with the opaque staged receipt; never wait for or expose processing. Reusing `SubmitGroupedIntake` preserves limits, source identity, replay, and the single Core policy owner.

## Governing docs

- **Meets and modifies `docs/frd/frd-09-provider-and-intermediary-routes.md`**: stable Principal isolation, same Core intake policies, idempotent instruction/attachment submission, provider actor attribution, and fail-closed behavior remain; the retired transient processing-status requirement is replaced by durable receipt plus API-03 terminal retrieval. The operator explicitly authorized this on 2026-08-21.
- Consume ADR-0030 from TICK-061; do not create another authentication or async-processing ADR.

## Steps

1. Merge/rebase the completed TICK-061 credential/authentication contract and confirm ADR-0030/FRD-09 are current.
2. Define the minimal Web wire contract: `POST /api/provider/submissions`, authenticated Principal, required `Idempotency-Key`, bounded multipart form with one JSON instruction part and ordered file parts, and HTTP 202 containing `receiptId` and `duplicate`.
3. Add a thin endpoint/adapter that validates transport shape, stamps `provider:{clientId}` as actor and a Principal-scoped source identity, then delegates to `IGroupedIntakeSubmission`; reuse repository upload size/count limits.
4. Map malformed/oversize input to 400/413, invalid or paused submission credentials to 401/403 without data disclosure, exact replay to the same 202 receipt, conflicting idempotency reuse to 409, and recoverable custody failure to bounded 503.
5. Compose the endpoint only when provider API configuration is explicitly enabled; a closed composition exposes no route. Do not perform live issuance or cloud writes.
6. Add contract/Core/integration/architecture tests for single/multiple files, ordering, limits, exact replay/conflict, Principal identity, actor history, pause/revoke, cross-Principal isolation, durable-before-response behavior, and absent route when disabled.
7. Refresh current architecture for the implemented caller, run the simplification lenses, then locked restore, Release build, focused/full tests, and record the post-implementation report.

## Verification

Authenticated Web integration tests submit representative repository fixtures and assert a durable staged receipt/work item before 202 returns, safe replay, no synchronous processing requirement, correct provider actor/Principal, and disabled-composition 404. No test contacts a live provider or mutates cloud services.

## Risks / open questions

- Memory pressure is bounded by the existing intake envelope; streaming beyond those limits is not introduced.
- AUTO-008 owns latency optimization, so this ticket does not shorten the dispatcher or bypass the SQL outbox.
- Live hostname, throttling values, and first-provider rollout remain activation gates.
