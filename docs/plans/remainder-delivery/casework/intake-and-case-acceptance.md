# Intake and case acceptance

## Purpose

Turn a durable, reviewable instruction or image intake into one accepted QDOS case draft without guessing identity or allocating a reference for pre-case material. This area owns the acceptance decision; [case identity and references](case-identity-and-references.md) owns reference semantics.

## Authority and current boundary

- **Authority:** [source order](../../../agent-guidance/source-of-truth.md), [questionnaire §§4–6](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), [remaining requirements §§1–4](../../remaining-requirements.md), and [ADR-0005](../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md).
- **Policy owner:** `ProcessQdosIntake` remains the single Core intake use case for Web and later Worker callers; a planned `AcceptCaseDraft` command owns the separate, authorised case-creation transaction after review.
- **Current implementation:** `ProcessQdosIntake`, `EfQdosIntakeStore`, `QdosIntakeReceiptEntity`, and `QdosTypedDraftEntity` form one local pre-case QDOS proof. Channel occurrence identity is idempotent, equal bytes may remain separate occurrences, and this path has no case/reference allocator.
- **Real callers:** `/Intake/Qdos` is the only current real intake caller, and only in Development with `Features:LocalQdosIntake`; Graph Worker, provider API, MCP and normal staff intake pages are **planned**.
- **Persistence/adapters:** current SQLite/SQL receipt, typed-draft, asset and audit tables. SHA-256 is integrity/possible-duplicate evidence rather than receipt identity; production custody remains later.
- **Dependencies:** the current local caller and extraction/provenance contracts are sufficient for the read-only relational-draft slice. Staff identity, durable custody, configured principal and [case identity](case-identity-and-references.md) are prerequisites only for confirmation and acceptance.
- **Replaces/consolidates:** replace the manual checkbox as the authority to create a case and global content-hash idempotency as receipt identity; extend rather than split or wrap `ProcessQdosIntake`, and retain hash as integrity and possible-duplicate evidence.

## Shared failure and observability rules

An uncertain classification, association, principal, vehicle registration, case type, identity-critical conflict, or standalone-Audit assessment is a retained pre-case outcome, never a best guess. `Needs sorting` means uncertain classification/association; staff-selected `Blocked intake` requires a reason and warning. Both retain source/provenance and allocate neither case nor reference. Persist correlation, actor, source identity and content-safe failure category; do not log source content. Every accept, block, retry and merge decision is permanently audited.

## Review and resolve an intake draft

**Evidence state:** Locally verified

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–6 and remaining requirements §§2–4.
- **Confirmed facts:** QDOS field candidates/provenance remain immutable JSON evidence, unambiguous values also persist in a relational read-only draft, and the original source is not durable first-MVP custody. Receipt processing creates no case/reference.
- **Decision required before the next mutation:** staff identity, operator confirmation, durable custody, principal configuration and the case-acceptance transaction remain separate planned boundaries. Worker/API source identity and Box custody are not a licence to call either system.

### Owner and dependencies

- **Policy/implementation owner:** existing Core `ProcessQdosIntake`, extended with one typed-draft persistence model; it remains the single intake entry point.
- **Independent evaluator:** test engineer with a frozen operator-reviewed QDOS cohort and author-hidden holdout.
- **Prerequisites:** current Development caller and typed extraction provenance. Authenticated actor/audit and durable production custody are required before confirmation or acceptance, not before a read-only local draft.
- **Consumers/unlocks:** reviewed staff UI, later Worker/API/MCP callers, case acceptance and inbox work views.

### Caller, contract and change boundary

- **Real or intended caller:** `/Intake/Qdos` is the current development-only caller of `ProcessQdosIntake` and remains so after refactoring; the staff review/accept action is **planned**. Later Web/Worker transports call the same intake use case and never reimplement classification.
- **Input/output:** channel plus immutable channel occurrence identity, extracted candidates/provenance and typed values produce either a read-only typed draft, `Needs sorting`, or another explicit pre-case outcome. Source identity is preserved after later matching.
- **Ordered decisions and failure behavior:** validate source occurrence identity; retain and classify; surface conflicts/missing data; produce typed values only when conversion is unambiguous. `Confirmed QDOS` remains a classification outcome; this slice records no operator confirmation, block, acceptance, case or reference.
- **Persistence/migration:** replace global content-hash identity with channel occurrence identity and relational typed draft values while retaining immutable candidate/provenance evidence. Replay of one occurrence is idempotent; equal bytes under different permitted identities remain separate evidence.
- **Adapters/side effects:** source/custody adapter supplies a persisted receipt; no Box, Graph, OCR, EVA or outbound message operation is performed by this task.
- **Operator surface and observability:** show the source, typed values, missing values, conflicts and provenance read-only. `Blocked intake`, corrections and confirmation remain absent until authenticated mutation exists.
- **Documentation affected:** keep operator notes read-only; amend implementation guidance only when the replacement is real.
- **Replaces/consolidates:** remove the current direct `ConfirmedQdos`-plus-checkbox creation branch and global hash-as-business-identity now, without replacing `ProcessQdosIntake` as the intake owner.

### Scope

- **Included:** QDOS relational typed draft, read-only review, provenance and channel-source-identity idempotency through the existing Development caller.
- **Excluded:** editing, confirmation, `Blocked intake`, case/reference creation, authenticated audit, automated mailbox receipt, provider API, OCR, DOC/MSG extraction, WhatsApp automation and case matching.

### Implementation checklist

- [x] Extend `ProcessQdosIntake` so the existing Development Web caller persists one source-identity receipt and relational typed read-only draft without a transport-specific alternative.
- [x] Retain immutable candidates/provenance and separate equal-content occurrences while making same-identity replay idempotent.
- [x] Remove the manual case-creation checkbox and raw-processing allocator; acceptance remains a separate later task.

### Validation checklist

- [x] Confirmed QDOS-shaped content becomes a read-only typed draft with every extracted field/provenance visible and no case/reference.
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
- **Confirmed facts:** a case is created by accepted definitive instruction or usable image-led intake; incomplete definitive instructions may become `Not ready`; complete instructions become `Review`.
- **Decision required before implementation:** None. The later report-sent freeze is explicitly withheld at [open decisions](../../open-decisions.md#authoritative-sent-report-evidence-and-time).

### Owner and dependencies

- **Policy/implementation owner:** planned Core `AcceptCaseDraft` transaction; [case identity](case-identity-and-references.md) supplies its allocator.
- **Independent evaluator:** separate test engineer; operator validates the QDOS acceptance journey.
- **Prerequisites:** reviewed typed draft, authenticated actor/audit and reference allocation transaction.
- **Consumers/unlocks:** lifecycle, workspace, Box custody outbox and EVA export plans.

### Caller, contract and change boundary

- **Real or intended caller:** planned authorised staff acceptance from the review page; `/Intake/Qdos` is the present development-only path to replace.
- **Input/output:** definitive draft plus staff-confirmed completeness yields exactly one QDOS case/reference and initial `Not ready` or `Review` state; otherwise retain pre-case item.
- **Ordered decisions and failure behavior:** authorise actor; revalidate definitive predicate; decide completeness; atomically create case, reference, source association, audit and outbox/custody work. Allocation exhaustion/concurrency failure is visible and leaves no partial case.
- **Persistence/migration:** one Core transaction across case identity, typed data, intake association, audit and outbox; no parallel editable receipt/case authority.
- **Adapters/side effects:** queue external custody after commit; an adapter failure blocks progression and is surfaced, never causes a second allocation.
- **Operator surface and observability:** show case/reference, initial queue and any custody-pending warning; audit actor, reason and correlation.
- **Documentation affected:** source-of-truth links and evidence record only after implementation.
- **Replaces/consolidates:** keep the retired raw-processing allocator absent; the future `AcceptCaseDraft` transaction becomes the sole owner of accepted case/reference creation.

### Scope

- **Included:** QDOS acceptance, initial state, transaction/idempotency and custody outbox boundary.
- **Excluded:** case lifecycle transitions, Box execution, report sending, assignment and external EVA changes.

### Implementation checklist

- [ ] Implement one acceptance use case with initial-state decision, audit and idempotent source association.
- [ ] Move reference allocation under that transaction and queue required custody work only after a committed case.
- [ ] Wire the replacement through the intended staff caller and delete the prior raw-processing allocation path.

### Validation checklist

- [ ] Complete and incomplete definitive instructions create one case in `Review` and `Not ready` respectively.
- [ ] Parallel/replayed acceptance consumes one sequence; failed allocation/custody preparation leaves no partially visible accepted case.
- [ ] Pre-case source never receives a reference; audit action has authenticated actor and required reason where applicable.
- [ ] Exercise the refactored `/Intake/Qdos` caller first; independently test SQL Server concurrency before release.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record its scoped result/limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Authorised complete definitive draft | One case/reference, `Review`, audit and custody work | transaction + caller test | Box write succeeded |
| Authorised incomplete definitive draft | One case/reference in `Not ready` | caller/integration test | Chaser cadence |
| Duplicate/concurrent acceptance | Original result returned; counter advances once | SQL concurrency test | Production load |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** QDOS case creation changes durable application state; operator acceptance is required before non-development activation. No cloud or external write is authorised here.
- **Rollout/activation:** deploy migration explicitly, activate staff caller after identity and custody gates, then run non-sensitive smoke input.
- **Rollback/recovery:** disable acceptance caller; preserve accepted cases/audit and repair through audited operations, never sequence reuse.
- **Irreversible risk:** reference allocation; counters and issued identities are never rewound.

### Deferred-capability impact

- **Named capabilities:** Diminution/Commercial, finance, EVA replacement/API, external accounts and guided capture.
- **Stable seam retained:** accepted case has case type, principal, origin, source association and typed fields without a provider-specific field matrix.
- **Future migration/replacement:** later case types/financial records need their own migrations and policy slices.
- **Activation boundary:** product decision and accepted design for each new case type/workflow.
- **Deliberately absent:** no financial values/workflows, external accounts, EVA API call or automatic report delivery.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Acceptance transaction boundary is defined | Implementation, caller, deployment or acceptance |
