# Intake and case acceptance

## Purpose

Turn a durable, reviewable instruction or image intake into one accepted QDOS case draft without guessing identity or allocating a reference for pre-case material. This area owns the acceptance decision; [case identity and references](case-identity-and-references.md) owns reference semantics.

## Authority and current boundary

- **Authority:** [source order](../../../agent-guidance/source-of-truth.md), [questionnaire §§4–6](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), [remaining requirements §§1–4](../../remaining-requirements.md), and [ADR-0005](../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md).
- **Policy owner:** `ProcessQdosIntake` remains the single Core receive/extract/classify use case for Web and later Worker/provider callers; a planned `AcceptCaseDraft` command owns the one authorised case-creation transaction for both automatic definitive instructions and staff-resolved/manual intake.
- **Current implementation:** `ProcessQdosIntake`, `EfQdosIntakeStore`, `QdosIntakeReceiptEntity`, and `QdosTypedDraftEntity` form one local pre-case QDOS proof. Channel occurrence identity is idempotent, equal bytes may remain separate occurrences, and this path has no case/reference allocator.
- **Real callers:** `/Intake/Qdos` is the only current real intake caller, and only in Development with `Features:LocalQdosIntake`; Graph Worker, provider API, MCP and normal staff intake pages are **planned**.
- **Persistence/adapters:** current SQLite/SQL receipt, typed-draft, asset and audit tables. SHA-256 is integrity/possible-duplicate evidence rather than receipt identity; production custody remains later.
- **Dependencies:** the current local caller and extraction/provenance contracts are sufficient for the read-only relational-draft slice. An audited staff or approved automation actor, durable custody, configured principal and [case identity](case-identity-and-references.md) are prerequisites for case creation.
- **Replaces/consolidates:** replace the manual checkbox as the authority to create a case and global content-hash idempotency as receipt identity; extend rather than split or wrap `ProcessQdosIntake`, and retain hash as integrity and possible-duplicate evidence.

## Shared failure and observability rules

An uncertain classification, association, principal, vehicle registration, case type, identity-critical conflict, or standalone-Audit assessment is a retained pre-case outcome, never a best guess. `Needs sorting` means uncertain classification/association; staff-selected `Blocked intake` requires a reason and warning. Both retain source/provenance and allocate neither case nor reference. Persist correlation, actor, source identity and content-safe failure category; do not log source content. Every accept, block, retry and merge decision is permanently audited.

## Review and resolve an intake draft

**Evidence state:** Locally verified

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–6 and remaining requirements §§2–4.
- **Confirmed facts:** QDOS field candidates/provenance remain immutable JSON evidence, unambiguous values also persist in a relational read-only draft, and the original source is not durable first-MVP custody. Receipt processing creates no case/reference.
- **Decision required before the next mutation:** actor/audit, durable custody, principal configuration and the case-acceptance transaction remain separate planned boundaries. Operator resolution is required only when the automatic predicate is not definitive; it is not a universal gate on new instructions. Worker/API source identity and Box custody are not a licence to call either system.

### Owner and dependencies

- **Policy/implementation owner:** existing Core `ProcessQdosIntake`, extended with one typed-draft persistence model; it remains the single intake entry point.
- **Independent evaluator:** test engineer with a frozen operator-reviewed QDOS cohort and author-hidden holdout.
- **Prerequisites:** current Development caller and typed extraction provenance. Authenticated actor/audit and durable production custody are required before confirmation or acceptance, not before a read-only local draft.
- **Consumers/unlocks:** reviewed staff UI, later Worker/API/MCP callers, case acceptance and inbox work views.

### Caller, contract and change boundary

- **Real or intended caller:** `/Intake/Qdos` is the current development-only caller of `ProcessQdosIntake` and remains so after refactoring. Later authorised Web/Worker/provider receipt paths call the same intake use case; Worker/provider definitive instructions automatically hand off to the single acceptance transaction, while staff review resolves only non-definitive or manual intake. Transports never reimplement classification or decide allocation.
- **Input/output:** channel plus immutable channel occurrence identity, extracted candidates/provenance and typed values produce either a read-only typed draft, `Needs sorting`, or another explicit pre-case outcome. Source identity is preserved after later matching.
- **Ordered decisions and failure behavior:** validate source occurrence identity; retain and extract; surface conflicts/missing data; produce typed values only when conversion is unambiguous. `QDOS draft` is an extraction outcome, not a mailbox category or definitive acceptance decision; this slice records no operator confirmation, block, acceptance, case or reference.
- **Persistence/migration:** replace global content-hash identity with channel occurrence identity and relational typed draft values while retaining immutable candidate/provenance evidence. Replay of one occurrence is idempotent; equal bytes under different permitted identities remain separate evidence.
- **Adapters/side effects:** source/custody adapter supplies a persisted receipt; no Box, Graph, OCR, EVA or outbound message operation is performed by this task.
- **Operator surface and observability:** show the source, typed values, missing values, conflicts and provenance read-only. `Blocked intake`, corrections and confirmation remain absent until authenticated mutation exists.
- **Documentation affected:** keep operator notes read-only; amend implementation guidance only when the replacement is real.
- **Replaces/consolidates:** do not restore the retired direct draft-plus-checkbox creation branch or global hash-as-business-identity; retain `ProcessQdosIntake` as the intake owner.

### Scope

- **Included:** QDOS relational typed draft, read-only review, provenance and channel-source-identity idempotency through the existing Development caller.
- **Excluded:** editing, confirmation, `Blocked intake`, case/reference creation, authenticated audit, automated mailbox receipt, provider API, OCR, DOC/MSG extraction, WhatsApp automation and case matching.

### Implementation checklist

- [x] Extend `ProcessQdosIntake` so the existing Development Web caller persists one source-identity receipt and relational typed read-only draft without a transport-specific alternative.
- [x] Retain immutable candidates/provenance and separate equal-content occurrences while making same-identity replay idempotent.
- [x] Remove the manual case-creation checkbox and raw-processing allocator; acceptance remains a separate later task.

### Validation checklist

- [x] Readable QDOS-shaped content becomes a read-only typed draft with every extracted field/provenance visible and no case/reference; uncertain category remains unresolved.
- [x] Missing registration, contradictory identity and ambiguous/unsupported content remain pre-case with no counter mutation.
- [x] Same source occurrence identity replay is idempotent; equal content under different permitted source identities remains reviewable evidence, not silently deleted.
- [ ] Exercise `/Intake/Qdos` against a frozen, operator-reviewed field-expectation cohort and untouched holdout. The genuine local smoke passed, but it does not establish field-level accuracy or business acceptance.
- [x] Run `pwsh ./scripts/Invoke-RepoCheck.ps1 -RequireCorpusEvidence`; record command, exit result and unrelated concurrent-tree limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| QDOS-shaped instruction | Relational read-only typed draft and immutable source provenance; no case/reference | UI/integration test and cohort result | staff confirmation or Graph/Box custody |
| Missing/contradictory identity | Retained warning or pre-case outcome; no case/reference | negative integration test | staff judgement accuracy |
| Same identity replay and equal-content new occurrence | Replay returns the original receipt; the new occurrence stays separately reviewable | persistence test | external delivery idempotency |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** no external mutation and no case/reference creation occurs in this slice.
- **Rollout/activation:** migrate data, enable only the refactored Development caller, prove it, then enable confirmation/acceptance and subsequent callers in their own approved slices.
- **Rollback/recovery:** retain receipts/candidates and disable the new caller; return unresolved items to visible pre-case review. Never delete a source or case.
- **Irreversible risk:** accepting a false case/reference; mitigate with the definitive predicate, transactional acceptance and independent holdout evidence.
- **Local-only residual:** two simultaneous first uses of one new token with different bytes are rejected at persistence, but the losing content-addressed local file may be unreferenced. Durable staging must reserve identity or reconcile orphan content before any production caller is enabled.

### Deferred-capability impact

- **Named capabilities:** later full mailbox coverage, WhatsApp, provider API/MCP, OCR/vision, legacy DOC/MSG and external accounts.
- **Stable seam retained:** source identity, original-source provenance, the single `ProcessQdosIntake` intake use case and the separate authorised acceptance transaction allow later transport adapters without another intake engine.
- **Future migration/replacement:** those adapters and credentials still require their own accepted slices and live evidence.
- **Activation boundary:** cohort/holdout results plus explicit operator approval for a wider automated acceptance rule.
- **Deliberately absent:** no dormant transport, queue, API endpoint, OCR client, WhatsApp integration or external-user account.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| `pwsh ./scripts/Invoke-RepoCheck.ps1 -RequireCorpusEvidence` | Exit 0: Core 11/11, integration 57/57, architecture 29/29, genuine corpus 11/11; no failures/skips | Development Web caller, populated SQL Server/SQLite upgrades, EF persistence, source/asset retention, route guards, repository structure and Bicep compilation | Local caller persists/replays typed pre-case drafts without case/reference schema; altered-byte token reuse fails closed; legacy local proof identity survives as audit evidence; supported genuine formats still traverse the caller | Operator-reviewed field accuracy/holdout, production custody, authentication, Worker, external adapters, Azure deployment or business acceptance |

## Accept a definitive case transaction

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–5 and remaining requirements §4.
- **Confirmed facts:** automatic case creation on receipt of a definitive authorised new instruction is first-MVP scope. Missing non-identity details do not prevent the case: an automatically created incomplete case begins in `Not ready`; `Review` requires staff-confirmed completeness. A definitive match associates image-led evidence without allocating a second case/reference.
- **Decision required before implementation:** None. The later report-sent freeze is explicitly withheld at [open decisions](../../open-decisions.md#authoritative-sent-report-evidence-and-time).

### Owner and dependencies

- **Policy/implementation owner:** planned Core `AcceptCaseDraft` transaction; [case identity](case-identity-and-references.md) supplies its allocator.
- **Independent evaluator:** separate test engineer; operator validates the QDOS acceptance journey.
- **Prerequisites:** durable processed draft, authorised audited staff or automation actor, accepted automatic predicate, configured principal and reference-allocation transaction. Staff review is a prerequisite only for manual/resolved intake or completeness confirmation.
- **Consumers/unlocks:** lifecycle, workspace, Box custody outbox and EVA export plans.

### Caller, contract and change boundary

- **Real or intended caller:** `ProcessQdosIntake` automatically hands a definitive authorised Worker/provider instruction to `AcceptCaseDraft`; authenticated Web submission/resolution calls the same transaction for staff-initiated work. `/Intake/Qdos` remains only the current development pre-case proof until those dependencies exist.
- **Input/output:** a definitive instruction plus authorised source/actor yields exactly one QDOS case/reference in `Not ready`, unless an audited staff completeness confirmation already permits `Review`. Manual resolution/retry uses the same command. A definitive match to an existing image-led/case record associates the source and allocates no duplicate.
- **Ordered decisions and failure behavior:** authorise the staff/service actor; re-read receipt, draft and acceptance evidence; require `Receiving work` or an authenticated principal-scoped provider instruction; require known principal/code, VRM, unambiguous case type, no identity-critical conflict and no unresolved association. A standalone Audit also requires an unambiguous original-report repairable/total-loss assessment. Idempotently return an existing acceptance/association, otherwise atomically create case, shared-sequence reference, source association, initial state, audit and custody outbox. Allocation exhaustion/concurrency/pre-commit failure is visible and leaves no partial case or counter movement. Post-commit custody failure retains the issued identity and retries without reallocation.
- **Persistence/migration:** one Core transaction across case identity, typed data, intake association, audit and outbox; no parallel editable receipt/case authority.
- **Adapters/side effects:** queue external custody after commit; an adapter failure blocks progression and is surfaced, never causes a second allocation.
- **Operator surface and observability:** show whether the case was created automatically or by staff resolution, its case/reference, initial queue and any custody-pending warning; audit actor, channel, predicate evidence/policy version, reason and correlation.
- **Documentation affected:** source-of-truth links and evidence record only after implementation.
- **Replaces/consolidates:** keep the retired raw-processing allocator absent; the future `AcceptCaseDraft` transaction becomes the sole owner of accepted case/reference creation.

### Scope

- **Included:** automatic creation for definitive authorised instructions, staff/manual resolution through the same QDOS acceptance transaction, initial state, existing-case association, idempotency and custody outbox boundary.
- **Excluded:** case lifecycle transitions, Box execution, report sending, assignment and external EVA changes.

### Implementation checklist

- [ ] Implement one acceptance use case plus one Core-owned definitive predicate; do not treat the current `DraftReady` extraction outcome as category or acceptance evidence sufficient to allocate a reference.
- [ ] Move reference allocation under that transaction and queue required custody work only after a committed case.
- [ ] Wire automatic Worker/provider hand-off and authenticated Web/manual resolution through that same transaction; delete the prior raw-processing allocation path and add no transport-specific acceptance branch.

### Validation checklist

- [ ] A definitive authorised instruction automatically creates one `Not ready` case despite missing non-identity data; only audited staff completeness confirmation permits `Review`.
- [ ] `DraftReady` with missing VRM, identity conflict, uncertain category/association/principal/case type, or missing standalone-Audit assessment allocates nothing. A staff-selected `Blocked intake` reason also allocates nothing until resolve/retry.
- [ ] A definitive existing-case/image-led match associates without another reference; uncertain matching remains `Needs sorting`.
- [ ] Parallel/replayed acceptance consumes one sequence; failed allocation/custody preparation leaves no partially visible accepted case.
- [ ] Pre-case source never receives a reference; audit action has authenticated actor and required reason where applicable.
- [ ] Exercise the refactored `/Intake/Qdos` caller first; independently test SQL Server concurrency before release.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record its scoped result/limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Authorised definitive Worker/provider instruction | One automatic case/reference in `Not ready`, audit and custody work | transaction + real-caller test | Box write succeeded |
| Staff confirms accepted case complete | Existing case moves to `Review`; no second reference | guarded Web caller test | later review outcome |
| Identity/category/association/Audit evidence is uncertain | Retained pre-case warning or `Needs sorting`; allocator is not called | negative caller tests | later staff judgement |
| Duplicate/concurrent acceptance | Original result returned; counter advances once | SQL concurrency test | Production load |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** QDOS case creation changes durable application state; operator acceptance is required before non-development activation. No cloud or external write is authorised here.
- **Rollout/activation:** deploy migration explicitly; prove staff/manual resolution and automatic predicates locally; obtain operator acceptance of the genuine definitive/false-case cohort; then activate only the approved caller. Worker activation additionally waits for the mailbox-category decision, and provider activation waits for its wire/auth contract.
- **Rollback/recovery:** disable acceptance caller; preserve accepted cases/audit and repair through audited operations, never sequence reuse.
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
