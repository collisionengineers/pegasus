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

## Simplification pass — 2026-08-28 (commit 387f5e26)

The entry above covers the CI-fix increment only. Commit 387f5e26 is titled
"simplification pass" but recorded no lenses and no dispositions; this is that
missing record, written from the commit's own diff.

- **Reuse.** Two dead records (`ProviderSubmissionMember`, the unused accepted
  -file projection) were deleted rather than left as a parallel vocabulary
  beside `ProviderSubmissionAcceptedFile` — one list per concept (applied).
- **Simplification.** The declared field labels were pinned to constants at
  their single owner instead of being restated at each call site (applied).
- **Efficiency.** No hot path changed; the pass was structural (n/a).
- **Altitude.** `ProviderInstructionPolicy` was left as the one normalisation
  owner; no wrapper was introduced to carry a value past a constraint (accept).

A second pass over the merge increment (this session) found nothing new: the
only changes are a delegation that removes a duplicated vocabulary, one test,
a merge resolution and a byte-order mark.

## Codex review dispositions — 2026-08-28

25 inline findings from `chatgpt-codex-connector` on PR #594, all raised
against head `ba3a0e92` — the **superseded multipart contract**. Each is
dispositioned below against the current head; none is silenced (AGENTS.md
rule 22). Verified means the current code was read this session.

### Fixed on this branch

| # | Finding | Disposition |
| --- | --- | --- |
| P1 | `IntakeContracts.cs:131` — Teach case-data reads about the provider source channel | **Fixed** in `c5011932`. `EfCaseDataStore` now delegates to `EfIntakeReceiptStore.ParseSourceChannel` instead of keeping a fifth copy of the vocabulary. Pinned by `ProviderApiSubmissionTests.AProviderCreatedCaseReadsItsDataSnapshotBack`, which fails with `Unknown persisted intake source channel 'provider_api'` against the previous parser. |

### Moot by rewrite — the multipart surface no longer exists

The endpoint takes a declared JSON instruction with its files inline. There is
no `multipart/form-data`, no `form.Files`, and no `ReadFormAsync`, so these
findings describe code that was deleted.

| # | Finding | Disposition |
| --- | --- | --- |
| P2 | `ProviderApiEndpoints.cs:115` — Map multipart parsing failures to API problem responses | **Moot by rewrite**, and the underlying risk is closed: parsing is now `ProviderInstructionJson.Parse(body)` *inside* the handler's try/catch, mapped to 400. |
| P2 | `ProviderApiEndpoints.cs:117` — Reject files outside the documented multipart field | **Moot by rewrite.** There is no form field; files are a bounded array inside the parsed body. |
| P2 | `ProviderApiEndpoints.cs:117` — Reject excess file counts before copying file contents | **Moot by rewrite.** The body is bounded before it is read (`ContentLength` pre-check, then a streaming read capped at `MaximumProviderApiRequestLength`); the count is enforced by `RequireEnvelope` over the already-bounded body. |
| P1 | `ProviderSubmission.cs:308` — Defer publication until the entire envelope is retained | **Moot by rewrite.** One submission is now one receipt: there is a single `IIntakeSubmission.ExecuteAsync` call, so no member can be published while later members are still being retained. |
| P2 | `ProviderSubmission.cs:307` — Reject replays that change file metadata | **Moot by rewrite.** Idempotency is now decided over the retained request bytes (`IntakeSourceIdentityConflictException` → 409), not by per-file hash comparison, so a changed filename or media type changes the body and conflicts. |
| P2 | `ProviderSubmission.cs:336` — Translate replayed file-count changes to 409 | **Moot by rewrite**, same reason: a changed file count changes the body, and the conflict is raised and mapped to 409. |
| P2 | `ProviderSubmission.cs:356` — Return the original duplicate flags on replay | **Moot by rewrite.** Duplicate flags are computed from the retained envelope by `AcceptedFiles`, not carried out of the grouped helper's replay-local state. |
| P2 | `ProviderSubmission.cs:282` — Bind the idempotency key before retaining the envelope | **Moot by rewrite.** The submission row is created before the single retention call and the source identity carries the submission token, so a retry with different bytes conflicts rather than being accepted as a first submission. |
| P2 | `ProviderSubmission.cs:299` — Preserve replay status during concurrent first submissions | **Addressed by rewrite.** The insert race is now caught explicitly (`ProviderSubmissionError.OperationConflict`) and re-read, so the loser resolves as a replay instead of both reporting 201. |

### Confirmed still live — escalated, not fixed here

These were verified against the current head this session. They are TICK-058's
own code but outside this lane's named files, so they are reported rather than
changed (see "Escalations" below and the PR description).

| # | Finding | Disposition |
| --- | --- | --- |
| P1 | `20260828111732_GrantProviderSubmissions.cs:35` — Grant UPDATE on ProviderSubmissions | **Confirmed live — merge blocker.** `EfProviderSubmissionStore.RecordStagedReceiptAsync` (lines 70-84) sets `StagedReceiptId` and calls `SaveChangesAsync`, an UPDATE. The migration grants the Web role `SELECT, INSERT` only, and the bootstrap census expects the same. Under the least-privilege SQL roles every successful POST would be refused after the intake source is already staged. Not reproduced by the tests because they run as the schema owner, not the runtime role. **Defer to the orchestrator: this must be fixed before the feature is activated anywhere.** |
| P1 | `Program.cs:372` — Rate-limit unverified key IDs by caller address | **Confirmed live — accepted risk until activation, deferred to a ticket.** The partition key is `ProviderApi.TryReadKeyId(...) ?? RemoteIpAddress`, and `TryReadKeyId` only checks the secret's *shape*. A caller who mints a fresh well-formed but unregistered key id per request therefore receives a new 60-requests-per-minute partition each time, so the limit does not bound credential enumeration or the rejected-credential `SecurityEvents` writes it provokes. The address fallback catches only absent or malformed headers. The real fix is a chained limiter — a per-address or global bucket over *pre-authentication* requests, with the per-key bucket applying after the credential verifies — which is a composition change beyond this lane. Risk is bounded today only because `Features:ProviderApi` is off and no credential exists; it must not be activated in this state. |
| P1 | `ProviderSubmission.cs:342` — Commit accepted history atomically with the submission | **Confirmed live — deferred to a ticket.** Execution order is `store.CreateAsync` → `intakeSubmission.ExecuteAsync` → `store.RecordStagedReceiptAsync` → `AppendHistoryAsync("Accepted")`. All three mutations are durable before the history write. If that write fails or the request is cancelled, the submission stands with no `Accepted` event, and a retry finds the existing row and records only `Replayed`, so the gap is never repaired — contrary to the permanent-history invariant in FRD-04. Compounds with the grant finding above: a denied UPDATE lands inside exactly this window. Fixing it properly means writing the history in the same transaction as the submission row, or repairing a missing `Accepted` on replay; both are Core changes this lane is not scoped for. |
| P1 | `ProcessIntake.cs:480` — Apply principal-scoped matching to provider submissions | **Confirmed live — escalated to the operator as a design question, deliberately unchanged.** See "Escalations". |
| P1 | `ProcessIntake.cs:758` — Recognize declared triage in the shared predicate | **Carried to review.** The declared-triage path sets `AcceptedTriageMatch` evidence with a null `MailClassificationDecision`; the finding is that `IsTriageRequest` then reports false and an Unidentified item is opened and never closed. `ADeclaredTriageOpensATriageAndAllocatesNoCase` passes, so the Triage is created — but that test does not assert the Unidentified queue is empty afterwards, which is precisely the gap the finding names. **Not disproved; needs the reviewer's judgement or a follow-up ticket.** |
| P2 | `CaseDataContracts.cs:28` — Label provider provenance in the case UI | **Confirmed live.** `OperatorLabels.Provenance` (lines 832-840) has arms for `StaffCorrection`, `IntakeEvidence`, `MailRoute`, `VehicleLookup`, `ProviderSetting` and `CaseAcceptance`, and falls through to `("Unknown", "icon-info")` — there is no `CaseDataSourceKind.ProviderApi` arm, though the kind is persisted and permitted by the check constraint. Every provider-declared field therefore shows "Unknown" provenance. **Not fixed here:** `OperatorLabels.cs` is shared by four EPIC-011 wave lanes this wave and this lane may not edit its existing switches. Report to the orchestrator for allocation. |
| P2 | `IntakeAllocation.cs:261` — Persist the provider principal in the case-data snapshot | **Confirmed live.** `CaseDataSnapshotFactory.AddProviderFact` returns early unless the receipt carries an accepted `MailRouteDecision` with a work-provider code. Provider receipts deliberately have no mail route, so `WorkProviderCode` is never written and the EVA export reports Work Provider as unrecorded even though allocation established the Principal. Defer to a ticket. |
| P2 | `ProviderSubmission.cs:317` — Require exactly one original report | **Confirmed live.** `RequireOriginalReport` uses `files.Any(file => file.Role == DocumentSemanticRole.AuditReport)`, so two files may claim the role; both then take the fixed `provider-original-report` label and the downstream single-match lookup fails the whole accepted intake instead of returning a validation error. A one-line change from `Any` to an exactly-one check, but in a file this lane was not asked to touch. Defer. |

### Carried to review — not verified this session

Raised against the superseded contract, plausibly surviving in altered form.
None was checked against the current head, and none is claimed closed.

| # | Finding | Disposition |
| --- | --- | --- |
| P1 | `DependencyInjection.cs:204` — Wire credential administration to a production caller | **Belongs to TICK-061 / PLAT-028**, not this ticket: the admin Principal-settings dialog that issues the key is EPIC-011 wave 4 (PLAT-051). Link, do not absorb (rule 2). Until it lands, API-04 has no production caller and the API cannot be used by a real provider — which is consistent with the gate being off. |
| P2 | `ProviderApiEndpoints.cs:115` — Reject paused credentials before parsing the body | **Partly addressed; residual accepted.** 413 and 415 now precede the read and the read is bounded and streaming, but `MaySubmit` is still checked inside `SubmitProviderInstruction` *after* the body is read and parsed, so a paused caller can still force a bounded read. Not "moot by rewrite" — recorded honestly as surviving. |
| P2 | `ProviderApiAuthenticationHandler.cs:106` — Persist denial events independently of request cancellation | **Carried to review.** Not re-verified; the handler was not rewritten. |
| P2 | `ProcessIntake.cs:750` — Default instruction dates from receipt time | **Carried to review.** |
| P2 | `IntakeAllocation.cs:295` — Retry the provider note after allocation | **Carried to review.** |
| P2 | `ProviderInstructionJson.cs:74` — Return field names for enum validation errors | **Carried to review.** This one is against the *new* file and was never re-reviewed, because the rewrite has had no review pass at all. |
| P2 | `ProviderSubmission.cs:269` — Reject Windows-style path components on Linux | **Carried to review.** |

### The review itself is stale

Every finding above predates the contract rewrite, and **no codex or human
review has run against the current head**. The dispositions here are the
implementer's own and are not a substitute for the independent review AGENTS.md
step 5 requires before this PR may merge.

## Escalations — operator/orchestrator decisions, deliberately unchanged

1. **`ProcessIntake.cs:478-498` — a provider submission returns before
   `EvaluateIntakeCaseMatch`.** The `provider_api` arm returns
   `DeclaredAssessment(...)` directly, so declared instructions never reach
   existing-case matching. A repeat instruction on the same claim therefore
   allocates a **new** case instead of matching the existing one. This may be
   intended — a declared instruction is definitive and states its own claim
   number — but it is a behavioural boundary no document settles, and codex
   raised it as a P1 duplicate-case risk. **Not changed. Needs the operator's
   decision.**

2. **`IntakeEnvelopeLimits` — 30 MiB decoded / 42 MiB body.** Chosen to carry a
   base64 instruction with its files inline. The post-implementation report
   already flags these as "still wants operator confirmation"; they remain
   unconfirmed.
