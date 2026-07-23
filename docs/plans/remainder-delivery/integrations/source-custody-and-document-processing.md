# Source custody and document processing

## Purpose

Replace local ignored artifact retention with durable, reviewable original-source custody without allowing a document reader, OCR adapter, or storage adapter to decide case workflow.

## Authority and current boundary

- **Authority:** [remaining requirements](../../remaining-requirements.md#2-reviewable-extraction-and-source-custody), [ADR-0005](../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md), and [ADR-0002](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md#files-box-and-document-processing).
- **Policy owner:** the named Core receive/process/accept intake operations; Core retains format, provenance and failure outcomes, not vendor types.
- **Current implementation:** `ProcessQdosIntake`, `MimeKitPdfPigQdosSourceReader`, and `FileSystemIntakeArtifactStore` support only the Development `/Intake/Qdos` caller and ignored local retention.
- **Real callers:** `/Intake/Qdos` is called only in Development when its feature flag is enabled. Worker receipt handling, Blob staging, Box custody and OCR are planned.
- **Persistence/adapters:** current receipt/asset metadata is SQL-backed; private Blob, one Box adapter and Document Intelligence are absent.
- **Dependencies:** the single Core receipt/draft spine and staff actor contracts. Box consumes the later custody outbox; it is not a prerequisite for durable staging.
- **Replaces/consolidates:** replace local artifact storage only after durable staging and confirmed Box custody; retain one engine-neutral source-reader contract.

## Shared failure and observability rules

An unreadable, incomplete, encrypted, corrupt, unsupported, bounded-out or pre-acceptance staging/scope-preparation failure remains visible with immutable identity and allocates no case/reference. After an accepted case/reference commits with custody outbox work, a Box failure retains that issued identity, blocks progression/Blob removal and retries idempotently; the sequence is never undone or reused. Logs and queue messages contain IDs and correlation, never document content.

## Durable source receipt, processing and custody hand-off

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [source custody](../../remaining-requirements.md#2-reviewable-extraction-and-source-custody), [format/candidate rules](../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md), and the approved Box custody sequence recorded below.
- **Confirmed facts:** local content-addressed artifacts do not prove Blob staging, Box custody, deployment or acceptance.
- **Decision required before implementation:** none for the stated staged custody path; each Box action still needs the exact approval in [Box case files](box-case-files.md#scoped-box-folder-and-version-custody).

### Owner and dependencies

- **Policy/implementation owner:** Core intake owner; Infrastructure owns Blob/OCR translations and the Box owner owns its adapter.
- **Independent evaluator:** test engineer, then a separate reviewer.
- **Prerequisites:** relational source-identity receipt for staging. Authenticated case acceptance is required only for creating the later custody outbox item.
- **Consumers/unlocks:** Graph, provider submissions, manual uploads, document actions and EVA export.

### Caller, contract and change boundary

- **Real or intended caller:** planned Web/Worker/provider receive operations call one Core receive/process use case, then one custody outbox operation; no direct SDK call from a page, trigger or endpoint.
- **Input/output:** immutable source/attachment identity and bytes yield stored provenance, typed processing result and either confirmed custody or a visible blocked outcome.
- **Ordered decisions and failure behavior:** retain receipt and stage bytes first; reject scope violations before acceptance; write case/reference, audit and one custody outbox item atomically; block progression and Blob removal until folder/file/version/hash/root confirmation. Replay must not create another reference or file.
- **Persistence/migration:** evolve the existing receipt/evidence authority in one migration for immutable source identity, adapter result, Blob/Box confirmation and retry state; do not introduce a parallel editable document authority.
- **Adapters/side effects:** managed-identity private Blob buffer, targeted `prebuilt-read` only for persisted scan-like PDF candidates, and the scoped Box adapter. Blob removal follows confirmed custody plus seven-day completed-item retention.
- **Operator surface and observability:** show custody state, format outcome and retryable/permanent reason; audit actor/outcome and emit content-free correlation, duration and adapter-result telemetry.
- **Documentation affected:** architecture/requirements evidence only; operator notes remain read-only.
- **Replaces/consolidates:** remove the local-file path and its registration only after equivalent real-caller evidence; no second source-reader or retention path.

### Scope

- **Included:** durable original staging and every retained asset occurrence, bounded extraction/provenance, custody outbox state and recovery.
- **Excluded:** targeted OCR (a separate billed-adapter task), automated VRM OCR/VLM, malware scanning, OCR of ordinary images, cloud evaluation uploads, and any live Box call not explicitly approved.

### Implementation checklist

- [ ] Split the current flow into durable receive, process and accept operations with immutable source identities and correlated outbox records.
- [ ] Add Blob staging and typed reader outcomes through the existing Core contract; preserve existing DOCX/EML/PDF bounds. Add targeted OCR only as its own approved caller-backed task.
- [ ] Hand custody to the single guarded Box use case and remove local artifact registration only after parity.

### Validation checklist

- [ ] Prove a source and each asset occurrence persist provenance and remain reviewable through the actual caller.
- [ ] Prove a transient custody retry, permanent/unknown failure, duplicate replay and reference-allocation concurrency do not duplicate business effects.
- [ ] Prove incomplete/unsupported/corrupt content remains `Needs sorting` with no case/reference.
- [ ] Use a run-scoped genuine local corpus cohort and hidden holdout with Box and every cloud/vendor upload disabled; record hashes, manifest, machine/toolchain and drift.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`; record its exact exit result and concurrent-tree limitation.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Confirmed source then custody replay | One receipt/case effect and one confirmed Box version | focused persistence/integration tests and approved scope smoke | production reliability |
| Scan-like PDF candidate | only recorded candidate pages call OCR | adapter test | accuracy of OCR fields |
| Pre-acceptance staging/scope failure | source remains visible; no case/reference | actual caller negative test | a live vendor repair |
| Post-commit Box failure | issued case/reference remains; progression and Blob removal are blocked for idempotent retry | outbox integration test | live Box recovery |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Blob/OCR deployment, credentials/RBAC and every Box write need separate approval; the first Box action is limited to the root/operations named in [Box case files](box-case-files.md#scoped-box-folder-and-version-custody).
- **Rollout/activation:** migrate; deploy inactive adapter configuration; prove local negative boundaries; obtain exact approval; enable one caller; then capture approved smoke evidence.
- **Rollback/recovery:** disable the caller/outbox claims, retain Blob and SQL receipts for replay, and redeploy the prior artifact; never delete source, case or Box version.
- **Irreversible risk:** Box content/version creation; require exact target/action approval before it occurs.

### Deferred-capability impact

- **Named capabilities:** legacy DOC/MSG, AI/vision/VRM OCR, malware scanning, broader mail, WhatsApp and later infrastructure.
- **Stable seam retained:** immutable source/asset occurrences, hashes, parent provenance and engine-neutral reader results.
- **Future migration/replacement:** DOC converter activation replaces the deferred branch; a scanner or AI path needs its own selected service, approval and caller-backed evidence.
- **Activation boundary:** product/accuracy/cost evidence and explicit approval; no corpus material may leave the local evaluation boundary.
- **Deliberately absent:** scanner, model, extra runtime, queue, general rendering service, OCR widening, feature flag or vendor upload path for deferred capabilities.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | caller, recovery and approvals are specified | implementation, deployment, live custody and acceptance |

## Deferred legacy containers

Legacy DOC and MSG automation remains outside this first-release pack. The current caller retains the original occurrence with an explicit unsupported/deferred outcome and no case/reference. A future implementation requires separately accepted format scope, security/licence ownership, failure policy, ADR and genuine-input evidence; no converter, dependency, route, process or deployment unit is planned now.
