# Plan — TICK-058: Principal-scoped provider submission API

## Approach

After TICK-061 supplies credential lifecycle and verification, add the first provider authentication handler and submission endpoint together in the existing Web Container App. Translate one bounded request into the existing grouped durable-intake owner and return an opaque receipt only after durable acceptance. Do not wait for processing or return files.

## Governing docs

- Modify FRD-09 to settle the exact submission wire contract, Principal isolation, idempotency, receipt response, and disclosure-safe failures before implementation.
- ADR-0004 remains the accepted authentication boundary; no ADR number is reserved.
- API-02 stays retired. API-03 alone resolves the provider's own receipt to an actual linked Case/PO or failure.

## Steps

1. Resolve the exact route, credential presentation, media type/parts, idempotency representation, response schema/statuses, and safe error mappings in FRD-09. Do not treat earlier multipart/HTTP Basic/202 suggestions as accepted defaults.
2. Integrate TICK-061's verification port and compose one provider authentication handler only alongside this real route; staff cookies must not authenticate it.
3. Add a thin Web adapter that enforces existing file/count/size limits, stamps Principal/client actor and source identity, and delegates to `IGroupedIntakeSubmission`.
4. Preserve exact replay to the same durable receipt and fail closed on conflicting reuse, malformed/oversize requests, invalid/revoked credentials, paused submission permission, and custody failure.
5. Reuse Azure SQL/outbox, transport Queue, Function Worker, custody Storage, HTTPS ingress, managed identity, and Application Insights. Add an application-level per-credential throttle with values fixed by capacity evidence.
6. Add contract/integration/architecture tests for wire shape, single/multiple ordered files, limits, replay/conflict, actor/Principal isolation, pause/revoke, durable-before-response, throttle behavior, and absence when not activated.
7. Refresh current-state docs after deployment, run the simplification lenses, locked restore/build/focused/full tests, and record evidence.

## Azure decision

No APIM, Front Door/WAF, Service Bus, extra Function, Entra app registration, or new store is justified initially. Capacity-test the existing one-replica Container App before changing scale. Reconsider APIM only for measured multi-provider traffic, centralized gateway governance, or a concrete WAF/domain requirement.

## Verification

Web/SQL tests prove the durable receipt exists before response, authentication is Principal-scoped, replay is safe, pause blocks only submission, and no response exposes processing details, Case data, files, or reports.

## Deferred activation

Named provider, exact hostname/custom domain, final throttling values, capacity target, and live credential issuance require separate activation evidence/approval.

## Simplification pass

- 2026-08-28: run over the branch's own diff after the CI-fix increment
  (concurrent-insert fake, uncomposed-surface 404 gate, history-order clock);
  no earlier pass was recorded. Lenses and dispositions: (1) Program.cs now
  carries three alike `app.Use` absence-gate blocks — extracting a shared
  helper rejected; the inline gate is that file's established convention and
  each block guards a different flag and path set (existing convention
  wins). (2) The optional `IProviderSubmissionBindings` constructor
  parameters on `ProcessIntake`/`AllocateIntake` follow those constructors'
  existing optional-collaborator convention (accept). (3) Nothing else in
  the Core/Infra/Web slices is a second implementation or a speculative
  abstraction; the store, handler, and endpoint each extend one existing
  pattern (accept). The three CI fixes themselves were checked and left
  minimal: the fake keeps its own key==Id invariant, the 404 gate is one
  flag-scoped block, and the moving clock is one test's composition choice.
