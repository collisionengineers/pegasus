# Intake and case acceptance

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready `0.1.0-alpha.1` plan — later `Next`/`unallocated` matching and reviewed vision remain planned**

## Purpose

Turn a durable, reviewable instruction or image intake into one accepted QDOS case draft without guessing identity or allocating a reference for pre-case material. This area owns the acceptance decision; [case identity and references](case-identity-and-references.md) owns reference semantics.

## Feature coverage

Primary matrix IDs: `AI-05`, `CASE-11`, `CASE-12`, `DATA-01`, `INT-01`, `INT-08`, `INT-17`, `INT-18`, `INT-19`, `INT-20`, `INT-22`, `INT-23`, `INT-24`, `INT-25`, `INT-26`, `INT-27`, and `INT-28`. Their routes are [draft review](#review-and-resolve-an-intake-draft), [ordinary-image VRM reading](#read-vehicle-registration-from-ordinary-images), [provisional image identity](#establish-provisional-image-identity-before-acceptance), [reviewed provider reference preparation](#prepare-reviewed-provider-reference-data), [definitive acceptance](#accept-a-definitive-case-transaction), [`Next`/`unallocated` record matching](#`Next`/`unallocated`-match-image-led-and-instruction-led-records), and [`Next`/`unallocated` reviewed vision assistance](#`Next`/`unallocated`-assist-vehicle-image-and-damage-review). Allocation remains owned by the [maturity map](../../feature-maturity-map.md); this list is a route, not implementation evidence.

## Authority and current boundary

- **Authority:** [source order](../../../../agent-guidance/source-of-truth.md), [questionnaire §§4–6](../../../product/project-discovery-questionnaire.md), [remaining requirements §§1–4](../../../../product/qdos-alpha-gap.md), [ADR-0005](../../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md), and [ADR-0006](../../../../architecture/decisions/ADR-0006-provider-neutral-intake-with-contained-qdos-policy.md).
- **Policy owner:** `ProcessIntake` is the single Core receive/process use case for Web and later Worker/provider callers. One contained `QdosInstructionExtractionPolicy` owns QDOS recognition and typed suggestions; a planned `AcceptCaseDraft` command owns the one authorised case-creation transaction.
- **Current implementation:** `ProcessIntake`, the provider-neutral EF intake store, `IntakeReceiptEntity`, and `InstructionDraftEntity` form one local pre-case proof. Channel occurrence identity is idempotent, equal bytes may remain separate occurrences, and this path has no case/reference allocator.
- **Real callers:** `/Intake/Upload` is the only current real intake caller, and only in Development with `Features:LocalIntake`; Graph Worker, provider API, MCP and authenticated staff intake pages are **planned**.
- **Persistence/adapters:** current SQLite/SQL receipt, typed-draft, asset and receipt-event tables. SHA-256 is integrity/possible-duplicate evidence rather than receipt identity; production custody remains later.
- **Dependencies:** the current local caller and extraction/provenance contracts are sufficient for the read-only relational-draft slice. A staff or approved automation actor recorded in permanent action history, durable custody, configured principal and [case identity](case-identity-and-references.md) are prerequisites for case creation.
- **Replaces/consolidates:** the provider-neutral use case replaces the former QDOS-shaped receipt/storage/route spine without adding a second intake engine. Content hash remains integrity and possible-duplicate evidence rather than receipt identity.

## Shared failure and observability rules

An uncertain classification, association, principal, vehicle registration, case type, identity-critical conflict, or standalone-Audit assessment is a retained pre-case outcome, never a best guess. `Needs sorting` means uncertain classification/association; staff-selected `Blocked intake` requires a reason and warning. Both retain source/provenance and allocate neither case nor reference. `Triage` routes to its separate business workflow and never becomes a generic category or case state. Persist correlation, actor, source identity and content-safe failure category; do not log source content. Every accept, block, retry and merge decision enters permanent action history.

## Review and resolve an intake draft

**Evidence state:** Locally verified

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–6 and remaining requirements §§2–4.
- **Confirmed facts:** provider-neutral field candidates/provenance remain immutable versioned JSON evidence; an applicable QDOS policy result may persist unambiguous values in a relational read-only instruction draft and suggest QDOS. Non-QDOS or ambiguous input receives no principal suggestion. The original source is retained locally but is not durable `0.1.0-alpha.1` custody. Receipt processing creates no case/reference.
- **Decision required before the next mutation:** actor/action history, durable custody, principal configuration and the case-acceptance transaction remain separate planned boundaries. Operator resolution is required only when the automatic predicate is not definitive; it is not a universal gate on new instructions. Worker/API source identity and Box custody are not a licence to call either system.

### Owner and dependencies

- **Policy/implementation owner:** Core `ProcessIntake` owns intake orchestration and the one typed-draft persistence model; `QdosInstructionExtractionPolicy` owns only QDOS evidence and field rules.
- **Independent evaluator:** test engineer with a frozen operator-reviewed QDOS cohort and author-hidden holdout.
- **Prerequisites:** current Development caller and typed extraction provenance. Authenticated actor/action history and durable production custody are required before confirmation or acceptance, not before a read-only local draft.
- **Consumers/unlocks:** reviewed staff UI, later Worker/API/MCP callers, case acceptance and inbox work views.

### Caller, contract and change boundary

- **Real or intended caller:** `/Intake/Upload` is the current development-only caller of `ProcessIntake`. Later authorised Web/Worker/provider receipt paths call the same use case; transports never reimplement extraction, mailbox classification, acceptance, or allocation.
- **Input/output:** channel plus immutable channel occurrence identity, extracted candidates/provenance and typed values produce either a read-only typed draft, `Needs sorting`, or another explicit pre-case outcome. Source identity is preserved after later matching.
- **Ordered decisions and failure behavior:** validate occurrence identity; retain the original source; read it; stop on unsupported, incomplete, OCR-required, or technical outcomes; invoke the QDOS policy only for fully readable input; surface conflicts/missing data; and create typed values only for an applicable, unambiguous result. `Draft ready` is an extraction outcome, not a mailbox category or definitive acceptance decision. A filename or sender never defaults the principal to QDOS.
- **Persistence/migration:** replace global content-hash identity with channel occurrence identity and relational typed draft values while retaining immutable candidate/provenance evidence. Replay of one occurrence is idempotent; equal bytes under different permitted identities remain separate evidence.
- **Adapters/side effects:** the local artifact adapter retains source bytes before any reviewable receipt is persisted. Retention failure is retryable and stores no receipt; a later SQL failure may leave reusable content-addressed bytes. No Box, Graph, OCR, EVA or outbound message operation is performed.
- **Operator surface and observability:** show the source, typed values, missing values, conflicts and provenance read-only. `Blocked intake`, corrections and confirmation remain absent until authenticated mutation exists.
- **Documentation affected:** keep operator notes read-only; amend implementation guidance only when the replacement is real.
- **Replaces/consolidates:** do not restore the retired direct draft-plus-checkbox creation branch, global hash-as-business-identity, former QDOS-shaped intake spine, or `/Intake/Qdos` compatibility route.

### Scope

- **Included:** provider-neutral receipt/review/provenance, channel-source-identity idempotency, and a QDOS-derived relational typed instruction draft through the existing Development caller.
- **Excluded:** editing, confirmation, `Blocked intake`, case/reference creation, authenticated action history, automated mailbox receipt, provider API, OCR, DOC/MSG extraction, WhatsApp automation and case matching.

### Implementation checklist

- [x] Use one provider-neutral `ProcessIntake` entry point so the Development Web caller persists one source-identity receipt and, only for applicable QDOS evidence, one relational typed read-only draft.
- [x] Retain immutable candidates/provenance and separate equal-content occurrences while making same-identity replay idempotent.
- [x] Remove the manual case-creation checkbox and raw-processing allocator; acceptance remains a separate later task.

### Validation checklist

- [x] Readable QDOS-shaped content becomes a read-only typed draft with every extracted field/provenance visible and no case/reference; uncertain category remains unresolved.
- [x] Missing registration, contradictory identity and ambiguous/unsupported content remain pre-case with no counter mutation.
- [x] Same source occurrence identity replay is idempotent; equal content under different permitted source identities remains reviewable evidence, not silently deleted.
- [ ] Exercise `/Intake/Upload` against a frozen, operator-reviewed field-expectation cohort and untouched holdout. The genuine local smoke passed, but it does not establish field-level accuracy or business acceptance.
- [x] Run `pwsh ./scripts/Invoke-RepoCheck.ps1 -RequireCorpusEvidence`; record command, exit result and unrelated concurrent-tree limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| QDOS-shaped instruction | Relational read-only typed draft and immutable source provenance; no case/reference | UI/integration test and cohort result | staff confirmation or Graph/Box custody |
| Missing/contradictory identity | Retained warning or pre-case outcome; no case/reference | negative integration test | staff judgement accuracy |
| Same identity replay and equal-content new occurrence | Replay returns the original receipt; the new occurrence stays separately reviewable | persistence test | external delivery idempotency |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** no external mutation and no case/reference creation occurs in this slice.
- **Rollout/activation:** use the new `artifacts/local/pegasus-development.db` provider-neutral baseline, enable only the refactored Development caller, prove it, then enable confirmation/acceptance and subsequent callers in their own approved slices. Old migration IDs or an unrecognised SQLite schema fail before mutation.
- **Rollback/recovery:** disable the new caller and restore the previous binary with its prior database path. Do not delete or down-migrate either database or retained artifacts; new-path receipts remain retained but unavailable to the reverted binary.
- **Irreversible risk:** accepting a false case/reference; mitigate with the definitive predicate, transactional acceptance and independent holdout evidence.
- **Local-only residual:** two simultaneous first uses of one new token with different bytes are rejected at persistence, but the losing content-addressed local file may be unreferenced. Durable staging must reserve identity or reconcile orphan content before any production caller is enabled.

### Deferred-capability impact

- **Named capabilities:** later full mailbox coverage, WhatsApp, provider API/MCP, OCR/vision, legacy DOC/MSG and external accounts.
- **Stable seam retained:** source identity, original-source provenance, the single `ProcessIntake` use case, contained extraction-policy contract, and separate authorised acceptance transaction allow later transports or approved providers without another intake engine.
- **Future migration/replacement:** those adapters and credentials still require their own accepted slices and live evidence.
- **Activation boundary:** cohort/holdout results plus explicit operator approval for a wider automated acceptance rule.
- **Deliberately absent:** no dormant transport, queue, API endpoint, OCR client, WhatsApp integration or external-user account.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| `pwsh ./scripts/Invoke-RepoCheck.ps1 -RequireCorpusEvidence` | Exit 0: Release build 0 warnings/errors; Core 28/28, non-corpus integration 82/82, architecture 30/30, genuine corpus 11/11; no failures/skips | Development `/Intake/Upload` caller, provider-neutral EF persistence, source/asset retention, strict SQLite baseline refusal, route guards, repository structure and Bicep compilation | Non-QDOS and transport-only QDOS evidence create no principal suggestion; local caller persists/replays typed pre-case drafts without case/reference schema; altered-byte token reuse and inconsistent policy results fail closed; supported genuine QDOS formats still traverse the caller | Operator-reviewed field accuracy/holdout, production custody, authentication, Worker, a live database upgrade, Azure deployment or business acceptance |
| `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --no-build --no-restore --filter "Category=SqlServer"` | 11/11 passed; no failures/skips | Disposable LocalDB applies the single provider-neutral initial migration explicitly, including constraints, concurrency, receipt-event rollback and retry | Fresh SQL Server schema and bounded transaction behavior for the test fixture | Azure SQL, a populated historical upgrade, production contention, release execution or restore |

## Read vehicle registration from ordinary images

**Evidence state:** Planned

`INT-17` adds a mechanism-neutral image-led caller to the existing Core intake boundary. It reads a registration only from ordinary vehicle images, preserves source/provenance and confidence or refusal, and fails closed to retained pre-case work when unreadable, conflicting, or uncertain. A genuine reviewed image cohort and untouched holdout must exercise that caller before any automatic use. It neither identifies a principal nor calls `AcceptCaseDraft`, allocates a case/reference, or introduces a separate vision/intake engine; any later billed adapter has its own approval, retry, cost and rollback evidence.

## Establish provisional image identity before acceptance

**Evidence state:** Planned

`INT-27` permits an unambiguous readable registration to form only a provisional pre-case image identity. The planned Core intake policy may use it to retain, display and later associate image-led evidence; principal, case type and any standalone-Audit evidence remain independently required before formal allocation, but a formal instruction is not an image-led creation prerequisite. It cannot create or reopen a case, allocate/reuse a reference, or bypass the remaining acceptance gates. A mistaken provisional association stays reversible with reasoned history; a definitive match associates evidence once and allocates no duplicate identity.

## Prepare reviewed provider reference data

**Evidence state:** Planned

`DATA-01` is a one-time, authorised reviewer procedure for transforming supplied provider spreadsheets into versioned local reference data with source provenance, review outcome and reproducible validation. It is not a product caller, runtime importer, upload/API surface, job, sync, or spreadsheet adapter. `ProcessIntake` may consume only the accepted prepared output; an absent, rejected or ambiguous record remains reviewable pre-case work and never defaults a principal or accepts a case. Replacing the prepared set needs explicit review, rollback to the prior accepted version and no silent rewriting of issued identities.

## Accept a definitive case transaction

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–5 and remaining requirements §4.
- **Confirmed facts:** automatic case creation on receipt of a definitive authorised new instruction is `0.1.0-alpha.1` scope. Missing non-identity details do not prevent the case: every automatically created instruction-led case begins incomplete in `Not ready`; it cannot be created directly in `Review`. A case may also begin from vehicle images: a readable registration is its provisional identity, and the formal Case/PO is allocated through this same transaction only once the principal and other identity gates are known. A definitive match associates image-led evidence without allocating a second case/reference.
- **Decision required before implementation:** None. Case principal/reference are immutable immediately on allocation; report evidence does not govern identity changes.

### Owner and dependencies

- **Policy/implementation owner:** planned Core `AcceptCaseDraft` transaction; [case identity](case-identity-and-references.md) supplies its allocator.
- **Independent evaluator:** separate test engineer; operator validates the QDOS acceptance journey.
- **Prerequisites:** durable fully processed source evidence, authorised staff or automation actor with permanent action history, configured principal, an unambiguous active case type, a readable registration, any standalone-Audit assessment evidence, and the reference-allocation contract. An instruction-led automatic caller also requires an accepted definitive-instruction predicate; image-led intake does not require a formal instruction. Staff review is a prerequisite only for non-definitive/manual resolution or later completeness confirmation.
- **Consumers/unlocks:** lifecycle, workspace, Box custody outbox and EVA export plans.

### Caller, contract and change boundary

- **Real or intended caller:** a future accepted-category flow from `ProcessIntake` may hand a definitive authorised Worker/provider instruction to `AcceptCaseDraft`; an authenticated Web intake action calls the same transaction for staff-resolved instruction-led work or image-led work whose principal and identity gates are known. `/Intake/Upload` remains only the current development pre-case proof until those dependencies exist.
- **Input/output:** a definitive instruction plus authorised source/actor yields exactly one incomplete QDOS case/reference in `Not ready`. Image-led source evidence with a readable registration yields exactly one incomplete `Not ready` case/reference only once the principal and other identity gates are known; no formal instruction is required. Manual resolution/retry uses the same command. A later explicit completeness confirmation may move the existing case to `Review`, never cause automatic instruction intake to create it there. A definitive match to an existing image-led/case record associates the source and allocates no duplicate.
- **Ordered decisions and failure behavior:** authorise the staff/service actor; re-read receipt, source, draft and acceptance evidence; require either an accepted definitive authorised instruction or authorised image-led intake with readable registration and known principal; require known principal/code, VRM, unambiguous case type, no identity-critical conflict and no unresolved association. A standalone Audit also requires an unambiguous original-report repairable/total-loss assessment. Route business `Triage` to its separate owner and allocate no case through this command. Idempotently return an existing acceptance/association, otherwise atomically create case, shared-sequence reference, source association, initial `Not ready` state, action-history entry and custody outbox. Allocation exhaustion/concurrency/pre-commit failure is visible and leaves no partial case or counter movement. Post-commit custody failure retains the issued identity and retries without reallocation.
- **Persistence/migration:** one Core transaction across case identity, typed data, intake association, action history and outbox; no parallel editable receipt/case authority.
- **Adapters/side effects:** queue external custody after commit; an adapter failure blocks progression and is surfaced, never causes a second allocation.
- **Operator surface and observability:** show whether the case was created automatically or by staff resolution, its case/reference, initial queue and any custody-pending warning; record actor, channel, predicate evidence/policy version, reason and correlation in permanent action history.
- **Documentation affected:** source-of-truth links and evidence record only after implementation.
- **Replaces/consolidates:** keep the retired raw-processing allocator absent; the future `AcceptCaseDraft` transaction becomes the sole owner of accepted case/reference creation.

### Scope

- **Included:** automatic creation for definitive authorised instructions, staff/manual resolution and image-led creation through the same QDOS acceptance transaction, initial `Not ready` state, existing-case association, idempotency and custody outbox boundary.
- **Excluded:** case lifecycle transitions, Box execution, report sending, assignment and external EVA changes.

### Implementation checklist

- [ ] Implement one acceptance use case plus one Core-owned definitive-instruction predicate and the settled image-led identity path; do not treat the current `DraftReady` extraction outcome as category or acceptance evidence sufficient to allocate a reference.
- [ ] Move reference allocation under that transaction and queue required custody work only after a committed case.
- [ ] Wire automatic Worker/provider hand-off and authenticated Web/manual resolution through that same transaction; delete the prior raw-processing allocation path and add no transport-specific acceptance branch.

### Validation checklist

- [ ] A definitive authorised instruction automatically creates one `Not ready` case despite missing non-identity data; only staff completeness confirmation in permanent action history permits `Review`.
- [ ] Image-led work with readable registration creates one `Not ready` case through the same transaction when the principal and remaining identity gates are known, without inventing an instruction requirement.
- [ ] `DraftReady` with missing VRM, identity conflict, uncertain category/association/principal/case type, or missing standalone-Audit assessment allocates nothing. A staff-selected `Blocked intake` reason also allocates nothing until resolve/retry.
- [ ] A definitive existing-case/image-led match associates without another reference; uncertain matching remains `Needs sorting`.
- [ ] Parallel/replayed acceptance consumes one sequence; failed allocation/custody preparation leaves no partially visible accepted case.
- [ ] Pre-case source never receives a reference; action-history entry has authenticated actor and required reason where applicable.
- [ ] Exercise the `/Intake/Upload` caller first; independently test SQL Server concurrency before release.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record its scoped result/limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Authorised definitive Worker/provider instruction | One automatic case/reference in `Not ready`, action history and custody work | transaction + real-caller test | Box write succeeded |
| Authorised image-led intake with readable registration and known principal | One case/reference in `Not ready` through the shared transaction without a formal instruction | guarded Web/Core caller test | later instruction match |
| Staff confirms accepted case complete | Existing case moves to `Review`; no second reference | guarded Web caller test | later review outcome |
| Identity/category/association/Audit evidence is uncertain | Retained pre-case warning or `Needs sorting`; allocator is not called | negative caller tests | later staff judgement |
| Duplicate/concurrent acceptance | Original result returned; counter advances once | SQL concurrency test | Production load |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** QDOS case creation changes durable application state; operator acceptance is required before non-development activation. No cloud or external write is authorised here.
- **Rollout/activation:** deploy migration explicitly; prove staff/manual resolution and automatic predicates locally; obtain operator acceptance of the genuine definitive/false-case cohort; then activate only the approved caller. Worker activation additionally waits for the mailbox-category decision, and provider activation waits for its wire/auth contract.
- **Rollback/recovery:** disable acceptance caller; preserve accepted cases/action history and repair through recorded forward operations, never sequence reuse.
- **Irreversible risk:** reference allocation; counters and issued identities are never rewound.

### Deferred-capability impact

- **Named capabilities:** Diminution/Commercial, finance, EVA replacement/API, external accounts and guided capture.
- **Stable seam retained:** one channel-neutral definitive predicate and acceptance command yield an accepted case with case type, principal, origin, source association and typed fields without a provider-specific field matrix.
- **Future migration/replacement:** later case types/financial records need their own migrations and policy slices.
- **Activation boundary:** product decision and accepted design for each new case type/workflow.
- **Deliberately absent:** no financial values/workflows, external accounts, EVA API call or automatic report delivery.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Acceptance transaction boundary is defined | Implementation, caller, deployment or acceptance |

## `Next`/`unallocated` match image-led and instruction-led records

**Evidence state:** Planned

`INT-28` adds one Core association policy after `0.1.0-alpha.1` acceptance, not a second intake or allocation engine. It compares retained image-led and instruction-led evidence and may associate them automatically only when the accepted policy proves a definitive match. Non-definitive candidates remain explainable and require authorised staff confirmation; ambiguous, conflicting or failed matches remain visible and unassociated. Automatic or staff-confirmed association preserves each source origin and allocates neither a second case nor reference, while reasoned reversal retains permanent history. The `Next`/`unallocated` caller, definitive predicate, threshold/evaluation cohort, mistaken-match recovery and audit evidence require their own approved slice; no external lookup is implied.

## `Next`/`unallocated` assist vehicle image and damage review

**Evidence state:** Planned

`AI-05` is reviewed assistance only: a separately approved image/damage adapter may propose evidence to the existing intake/review policy, with its result, version and refusal visible to staff. It cannot decide principal, case type, completeness, matching, acceptance, allocation or lifecycle state; rules and staff confirmation remain authoritative. Its `Next`/`unallocated` activation gate is approved cost/data scope, evaluation cohort/holdout, failure/rollback behavior and permanent action-history boundary; unlike conditional `AI-02`, `AI-03`, `AI-04` and `AI-06`, it does not require evidence that deterministic rules are insufficient. No cross-domain AI owner, background activation or unreviewed case mutation is introduced.
