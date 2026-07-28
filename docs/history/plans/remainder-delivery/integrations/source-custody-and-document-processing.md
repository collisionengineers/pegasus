# Source custody and document processing

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready `0.1.0-alpha.1` custody plan — scan-like PDF OCR and DOC/MSG automation `Next`/`unallocated`**

## Purpose

Replace local ignored artifact retention with durable, reviewable original-source custody without allowing a document reader, OCR adapter, or storage adapter to decide case workflow.

## Feature coverage

Primary feature ownership is: `INT-09`, `INT-10`, `INT-11`, `INT-12`,
`INT-13`, `DOC-08`, `INT-14`, `INT-15`, and `INT-16`. Durable source custody
owns the `0.1.0-alpha.1` formats and transient staging; `Next`/`unallocated` legacy DOC/MSG automation and
`Next`/`unallocated` scan-like PDF OCR remain separate anchors below. Neither is automatic VRM
reading, image/damage assistance, or a general document/AI service.

## Authority and current boundary

- **Authority:** [remaining requirements](../../../../product/qdos-alpha-gap.md#2-reviewable-extraction-and-source-custody), [ADR-0005](../../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md), and [ADR-0002](../../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md#files-box-and-document-processing).
- **Policy owner:** the named Core receive/process/accept intake operations; Core retains format, provenance and failure outcomes, not vendor types.
- **Current implementation:** `ProcessIntake`, the registered `MimeKitPdfPigOpenXmlIntakeSourceReader`, and `FileSystemIntakeArtifactStore` support only the Development `/Intake/Upload` caller and ignored local retention. The reader processes every PDF page or marks the intake incomplete when its shared text/image expansion budget is breached; it does not silently truncate by page count.
- **Real callers:** `/Intake/Upload` is called only in Development when `Features:LocalIntake` is enabled. In the planned production path, Web is the first byte receiver for manual/provider HTTP submissions; Worker is the first byte receiver for Graph and consumes queued staged-source identifiers. Blob staging, Box custody and OCR adapters are not implemented.
- **Persistence/adapters:** current receipt/asset metadata is SQL-backed. Bicep declares private `intake-temporary` Blob storage, but the Web identity receives no data-plane role or container configuration until the Blob adapter and real caller are delivered together. No application Blob adapter/caller evidence, Box adapter or Document Intelligence adapter exists.
- **Dependencies:** the single Core receipt/draft spine and staff actor contracts. Box consumes the later custody outbox; it is not a prerequisite for durable staging.
- **Replaces/consolidates:** replace local artifact storage only after durable staging and confirmed Box custody; retain one engine-neutral source-reader contract.

## Shared failure and observability rules

An unreadable, incomplete, encrypted, corrupt, unsupported, bounded-out or pre-acceptance staging/scope-preparation failure remains visible with immutable identity and allocates no case/reference. After an accepted case/reference commits with custody outbox work, a Box failure retains that issued identity, blocks progression/Blob removal and retries idempotently; the sequence is never undone or reused. Logs and queue messages contain IDs and correlation, never document content.

## Durable source receipt, processing and custody hand off

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [source custody](../../../../product/qdos-alpha-gap.md#2-reviewable-extraction-and-source-custody), [format/candidate rules](../../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md), and the approved Box custody sequence recorded below.
- **Confirmed facts:** local content-addressed artifacts do not prove Blob staging, Box custody, deployment or acceptance.
- **Decision required before implementation:** none for the stated staged custody path; each Box action still needs the exact approval in [Box case files](box-case-files.md#scoped-box-folder-and-version-custody).

### Owner and dependencies

- **Policy/implementation owner:** Core intake owns receipt/custody policy; Infrastructure owns the single Blob translation; the targeted OCR task below owns its separate adapter; the Box owner owns its adapter.
- **Independent evaluator:** test engineer, then a separate reviewer.
- **Prerequisites:** relational source-identity receipt for staging. Authenticated case acceptance is required only for creating the later custody outbox item.
- **Consumers/unlocks:** Graph, provider submissions, manual uploads, document actions and EVA export.

### Caller, contract and change boundary

- **Real or intended caller:** planned Web manual/provider receive operations stage request bytes and atomically record the source receipt plus processing-outbox row before acknowledging receipt. A Worker-hosted SQL outbox dispatcher sends only the source ID to `intake-work`; Worker stages Graph bytes through the same receive operation and processes queued IDs. Both call one Core receive/process use case and one Infrastructure storage port; no page, trigger or endpoint calls the Blob SDK directly, and queues never carry source bytes.
- **Input/output:** immutable source/attachment identity and bytes yield stored provenance, typed processing result and either confirmed custody or a visible blocked outcome.
- **Ordered decisions and failure behavior:** idempotently stage bytes under the immutable occurrence key; atomically record the source receipt and processing outbox; acknowledge only after both commit; let the Worker dispatcher publish the source ID. A queue failure leaves the outbox pending for one-time later dispatch. A Blob staged before a failed SQL commit is recovered by retrying the same key; unresolved unreferenced objects are alerted/quarantined and may be removed only after the retention rule proves no receipt/outbox owns them. Reject scope violations before acceptance. Later, write case/reference, action-history entry and one custody outbox item atomically; block progression and Blob removal until folder/file/version/hash/root confirmation. Replay must not duplicate processing, reference or file.
- **Persistence/migration:** evolve the existing receipt/evidence authority in one migration for immutable source identity, adapter result, Blob/Box confirmation and retry state; do not introduce a parallel editable document authority.
- **Adapters/side effects:** managed-identity private Blob buffer and the scoped Box adapter. Web uses its system-assigned identity only on `intake-temporary`; Worker uses its own identity for Graph staging and background processing. The current single-account Functions design requires broader Worker host/deployment storage roles, so it does not provide container isolation from intake data. Blob removal follows confirmed custody plus seven-day completed-item retention.
- **Operator surface and observability:** show custody state, format outcome and retryable/permanent reason; record actor/outcome in permanent action history and emit content-free correlation, duration and adapter-result telemetry.
- **Documentation affected:** architecture/requirements evidence only; operator notes remain read-only.
- **Replaces/consolidates:** remove the local-file path and its registration only after equivalent real-caller evidence; no second source-reader or retention path.

### Scope

- **Included:** durable original staging and every retained asset occurrence, bounded extraction/provenance, custody outbox state and recovery.
- **Excluded:** the separately owned targeted OCR task below, automated VRM OCR/VLM, malware scanning, OCR of ordinary images, cloud evaluation uploads, and any live Box call not explicitly approved.

### Implementation checklist

- [ ] Split the current flow into durable receive, process and accept operations with immutable source identities and correlated processing/custody outbox records. The Worker-hosted SQL dispatcher, not Web, owns queue publication.
- [ ] Replace `FileSystemIntakeArtifactStore` in production composition with one Blob adapter reached through the existing Core port. Web stages manual/provider sources with its container-scoped identity; Worker stages Graph sources and consumes identifiers. Preserve the existing DOCX/EML bounds and all-pages-or-incomplete PDF budget across every caller.
- [ ] Hand custody to the single guarded Box use case and remove local artifact registration only after parity.

### Validation checklist

- [ ] Prove a source and each asset occurrence persist provenance and remain reviewable through the actual caller.
- [ ] Prove a transient custody retry, permanent/unknown failure, duplicate replay and reference-allocation concurrency do not duplicate business effects.
- [ ] Prove incomplete/unsupported/corrupt content remains `Needs sorting` with no case/reference.
- [ ] Through the real Web upload, prove managed-identity create/read in `intake-temporary`, denial against `app-package` and other containers, and no acknowledgement when durable staging fails.
- [ ] Prove a post-stage SQL failure is recovered through the same object key, and a post-receipt queue failure remains pending then dispatches once without lost or duplicate processing. Exercise unreferenced-object quarantine/retention without deleting an owned source.
- [ ] Use a run-scoped genuine local corpus cohort and hidden holdout with Box and every cloud/vendor upload disabled; record hashes, manifest, machine/toolchain and drift.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`; record its exact exit result and concurrent-tree limitation.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Confirmed source then custody replay | One receipt/case effect and one confirmed Box version | focused persistence/integration tests and approved scope smoke | production reliability |
| Pre-acceptance staging/scope failure | source remains visible; no case/reference | actual caller negative test | a live vendor repair |
| Post-commit Box failure | issued case/reference remains; progression and Blob removal are blocked for idempotent retry | outbox integration test | live Box recovery |
| Manual/provider Web receipt | Blob, receipt and processing outbox are durable before acknowledgement; Worker dispatcher later queues only the source ID | actual Web plus outbox-dispatch integration test | live Azure RBAC until approved smoke |
| Queue unavailable after receipt commit | outbox stays pending and later dispatches one source ID; Web still has no queue permission | persistence/dispatcher retry test | live queue availability |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Blob/OCR deployment, credentials/RBAC and every Box write need separate approval; the first Box action is limited to the root/operations named in [Box case files](box-case-files.md#scoped-box-folder-and-version-custody).
- **Rollout/activation:** migrate; deploy inactive adapter configuration; prove local negative boundaries; obtain exact approval; enable one caller; then capture approved smoke evidence.
- **Rollback/recovery:** disable the caller/outbox claims, retain Blob and SQL receipts for replay, and redeploy the prior artifact; never delete source, case or Box version.
- **Irreversible risk:** Box content/version creation; require exact target/action approval before it occurs.

### Deferred-capability impact

- **Named capabilities:** legacy DOC/MSG, AI/vision/VRM OCR, broader mail, WhatsApp, and later infrastructure. Malware scanning is `Not planned`, with no activation path or seam.
- **Stable seam retained:** immutable source/asset occurrences, hashes, parent provenance and engine-neutral reader results.
- **Future migration/replacement:** DOC converter activation replaces the deferred branch; a later general document reader may be external or developed in-house, but must replace the current reader through the engine-neutral port only after parity and caller-backed evaluation. An allocated AI path needs its own selected service, approval, and caller-backed evidence; no scanner migration exists.
- **Activation boundary:** product/accuracy/cost evidence and explicit approval; no corpus material may leave the local evaluation boundary.
- **Deliberately absent:** the `Not planned` scanner boundary plus any dormant model, extra runtime, queue, general rendering service, OCR widening, feature flag, or vendor upload path for later capabilities.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | caller, recovery and approvals are specified | implementation, deployment, live custody and acceptance |

## Targeted scanned PDF OCR

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [the maturity map](../../feature-maturity-map.md) allocates scan-like PDF OCR to `Next`/`unallocated`. Existing candidate detection may remain review evidence, but OCR execution is not a `0.1.0-alpha.1` gate. Eligibility remains limited to persisted pages with fewer than 80 non-whitespace embedded-text characters and a dominant raster covering at least 80 percent of the page.
- **Confirmed facts:** the Core contract records scan-like page candidates. The current Bicep conditionally declares Document Intelligence plus Worker `Cognitive Services User`, but no OCR adapter, caller or result persistence exists; the `0.0.0-development`/`0.1.0-alpha.1` platform plan must remove that dormant resource/role path before shared-development deployment. This `Next`/`unallocated` plan owns any later reintroduction as one separately approved activation change.
- **Decision required before implementation:** none for the local adapter/contract work. Any billed call, Azure enablement or deployment requires a direct approval naming account, region/SKU, non-corpus input/page limit and spending cap. Repository corpus is immutable local evidence and is never eligible for upload.

### Owner and dependencies

- **Policy/implementation owner:** Core document-processing policy owns candidate eligibility and typed outcomes; one Infrastructure Document Intelligence adapter owns `prebuilt-read` translation. Worker composition is the sole production OCR caller.
- **Independent evaluator:** a test engineer authors negative routing/retry cases; a different Azure/domain reviewer gives the verdict.
- **Prerequisites:** durable source/page-candidate persistence, Worker identity, bounded processing contract, accepted `0.1.0-alpha.1` release with no Document Intelligence resource/role/configuration, and a newly approved non-production service target for this `Next`/`unallocated` slice.
- **Consumers/unlocks:** Worker intake completion and operator review of scanned PDF text/provenance.

### Caller, contract and change boundary

- **Real or intended caller:** a named Worker handler claims a persisted scan-like page candidate and calls the same Core document-processing use case; Web records/displays status but never invokes Document Intelligence in an HTTP request.
- **Input/output:** persisted source identity, PDF identity and candidate page number yield page-scoped text/provenance plus success, retryable, permanent, bounded or unknown outcome; vendor response types do not enter Core.
- **Ordered decisions and failure behavior:** embedded extraction records the candidate first; Worker submits only eligible persisted pages; bounded transient retry ends visibly; incomplete/unknown/permanent results remain `Needs sorting` and allocate no case/reference. Replay cannot duplicate page results or downstream acceptance.
- **Persistence/migration:** extend the existing source/page evidence authority with attempt/result/provenance fields and an idempotent work key; do not create a second document or OCR authority.
- **Adapters/side effects:** Document Intelligence Read uses managed identity with local authentication disabled. No ordinary image, DOCX image, PDF embedded object, VRM crop or unpersisted byte stream is submitted.
- **Operator surface and observability:** show OCR-required/running/failed/completed status and page provenance; emit content-free correlation, page count, duration, attempt, outcome and billed-call metrics.
- **Documentation affected:** this task, ADR evidence and current Azure inventory only when live state changes; operator notes remain read-only.
- **Replaces/consolidates:** completes the existing `OcrRequired` branch through one adapter/caller; no second PDF parser, generic OCR service or image-recognition route.

### Scope

- **Included:** one `Next`/`unallocated` activation change that adds candidate-page dispatch, the exact Document Intelligence resource/role/configuration, `prebuilt-read` adapter, result persistence, bounded retry/idempotency, managed-identity configuration, content-safe telemetry and actual Worker caller evidence together.
- **Excluded:** automated VRM OCR/VLM, ordinary-image OCR, handwriting/semantic extraction promises, legacy DOC/MSG conversion, malware scanning, general rendering and every corpus/cloud upload.

### Implementation checklist

- [ ] Add the engine-neutral OCR request/result contract to the existing Core document-processing owner and persist page-level work/result provenance in the existing migration stream.
- [ ] Confirm the accepted `0.1.0-alpha.1` topology contains no Document Intelligence resource, role assignment, endpoint or dormant Worker configuration; introduce them only in this approved `Next`/`unallocated` activation change.
- [ ] Implement one Infrastructure Document Intelligence `prebuilt-read` adapter and register it only in Worker production composition; preserve page identity and existing intake-wide bounds.
- [ ] Add one idempotent Worker handler for persisted eligible page candidates with bounded retry and visible terminal/unknown outcomes; do not add a Web OCR caller.

### Validation and acceptance

- [ ] Prove eligible scan-like pages route once, while ordinary images, text-bearing pages, low-text pages without a dominant raster and deferred formats never call the adapter.
- [ ] Prove retry, timeout, malformed/partial response, cancellation and replay preserve the source and create no case/reference or duplicate result.
- [ ] Prove the Worker identity can call only the approved Document Intelligence resource and local auth is disabled; record page/cost telemetry without content.
- [ ] Prove genuine-corpus evaluation remains offline with the cloud adapter disabled and a spy adapter recording zero external calls.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`; any approved live smoke uses a named non-corpus input and separately records target, pages, cost cap and result.

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Persisted eligible PDF page | Worker records one page-scoped OCR result and processing resumes through Core | adapter/Worker integration test | extraction accuracy on genuine work |
| Ordinary image or ineligible PDF page | no vendor call; visible manual-review/current extraction outcome remains | negative spy-adapter test | future VRM OCR/VLM |
| Retry/replay or partial vendor result | bounded attempts, one durable outcome, no case/reference from incomplete input | persistence/handler integration test | live service reliability |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** enabling/deploying Document Intelligence or sending any page requires exact subscription, tenant, UK South resource/SKU, named non-corpus input, page ceiling and hard spending cap. Repository corpus cannot be approved for upload.
- **Rollout/activation:** keep the resource, role, endpoint/configuration, adapter registration and caller absent through `0.1.0-alpha.1`. After the `Next`/`unallocated` target/data/page/cost approval, introduce them together, prove negative routing locally, deploy one development Worker caller, run a bounded non-corpus smoke, then review cost/accuracy before wider use. Do not ship a dormant disabled resource or adapter in advance of that gate.
- **Rollback/recovery:** stop claims/disable the adapter and redeploy the prior artifact; retain source, candidates and attempts for manual review/replay.
- **Irreversible risk:** document content leaves the application boundary and incurs a billed vendor call; fail before submission when scope or approval is missing.

### Deferred-capability impact

- **Named capabilities:** automated VRM OCR/VLM, AI/vision assistance, legacy DOC/MSG, broader mailbox coverage, and later OCR/provider replacement. Malware scanning is `Not planned`, with no activation path or seam.
- **Stable seam retained:** engine-neutral page candidate/result contracts, immutable source/page identity and provider-neutral provenance/outcomes.
- **Future migration/replacement:** another approved OCR provider replaces the Infrastructure adapter and may require result-version migration; candidate policy remains in Core.
- **Activation boundary:** representative accuracy, licence/service, privacy, cost and operator approval for each widened input class.
- **Deliberately absent:** no generic rule engine, OCR microservice, image pipeline, dormant alternate provider, general page renderer or widened input route.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | owner, caller, billing gate, failures and negative scope are explicit | adapter, live OCR, deployment or operator acceptance |

## Automate legacy DOC and MSG

**Evidence state:** Planned

`INT-14` and `INT-15` are a `Next`/`unallocated` Core document-processing extension, called only
by the approved Worker receipt path after durable source retention. `0.1.0-alpha.1` retains
each original occurrence with an explicit unsupported outcome and no
case/reference. The implementation must use the existing engine-neutral
reader-result and source-occurrence boundary, preserve every visible failure
as an operator-visible outcome, and fail closed before allocation on
incomplete, ambiguous, corrupt, encrypted, bounded-out, or unsupported input.

The exact converter/runtime, licence and security ownership, isolation,
resource limits, and accepted format scope remain activation evidence; they do
not authorise a dormant converter, dependency, route, process, deployment
unit, OCR widening, or cloud upload in `0.1.0-alpha.1`. Focused format/negative/replay tests
and genuine local evidence through the actual Worker caller must establish
parity before this replaces the deferred branch. This anchor is deliberately
separate from [targeted scanned-PDF OCR](#targeted-scanned-pdf-ocr): DOC
and MSG automation neither calls OCR nor broadens its billed external adapter.
