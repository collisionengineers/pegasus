# Acceptance and cutover

## Purpose

Prove the complete QDOS workflow through its actual callers, keep corpus and live-system evidence boundaries separate, obtain operator acceptance, and cut over without widening external scope or destroying rollback.

## Authority and current boundary

- **Authority:** [First-release finish line](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#15-first-release-scope), [remaining requirements](../../remaining-requirements.md), [open decisions](../../open-decisions.md), and [evidence rules](../../../../AGENTS.md#evidence-and-validation).
- **Policy owner:** Each feature owns its product proof; the release owner composes the integrated journey without duplicating rules.
- **Current implementation:** The development-only `/Intake/Upload` path has local integration evidence. No deployed Worker, Graph, Box, provider API or MCP path is currently proven.
- **Real callers:** Existing Web intake first; later Web/Worker/API/MCP callers only after their task evidence reaches `Called`.
- **Persistence/adapters:** The acceptance record names input class, source hash/cohort, actor, database, external scope, artifact and result without publishing PII.
- **Dependencies:** Every required area in the [programme order](../README.md#delivery-order); open decisions block only their affected journey steps.
- **Replaces/consolidates:** Global test lists and broad green claims are replaced by task-local evidence plus one integrated acceptance record.

## Shared failure and observability rules

A test failure, unsupported source, integration failure and business outcome remain distinct. A repository pass cannot substitute for caller proof; a deployment cannot substitute for live verification; technical verification cannot substitute for operator acceptance. Corpus runs never call Box or another external service.

## Prove the local workflow with genuine inputs

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** Use genuine immutable local material for extraction and false-creation evidence while keeping it offline.
- **Confirmed facts:** Existing synthetic integration tests prove routes and bounded format handling, not operator-accepted field accuracy.
- **Decision required before implementation:** Human-reviewed expected values and an untouched holdout must be frozen before evaluator access.

### Owner and dependencies

- **Policy/implementation owner:** Feature owners expose the actual local caller; a test engineer owns the evaluation definition.
- **Independent evaluator:** Different agent from implementation and expected-value author runs the holdout and reports limitations.
- **Prerequisites:** Typed draft, acceptance, source retention and relevant lifecycle/UI slices.
- **Consumers/unlocks:** Shared-development deployment and operator acceptance.

### Caller, contract and change boundary

- **Real or intended caller:** Existing `POST /Intake/Upload`, followed by authenticated Web journeys as delivered.
- **Input/output:** Frozen genuine sources yield retained occurrences, reviewable candidates, explicit failures and no false case creation.
- **Ordered decisions and failure behavior:** Hash inventory, disable external calls, run cohort, correct implementation only against cohort, then run untouched holdout once; never treat unreadable/partial input as confirmation.
- **Persistence/migration:** Use the application store and exact test database provider named in results; preserve source hashes and evaluation lineage.
- **Adapters/side effects:** Local file adapter only; Box, Graph, OCR/vendor and Azure calls are disabled.
- **Operator surface and observability:** Record field outcomes, conflicts, unreadable pages, false confirmation/case creation and content-free timing.
- **Documentation affected:** Evaluation results belong under ignored `artifacts/`; accepted product decisions alone update canonical documents.
- **Replaces/consolidates:** No parser-only or selected-test result can claim end-to-end accuracy.

### Scope

- **Included:** Genuine EML/PDF/DOCX/image shapes, current DOC/MSG retention, targeted scan candidate decisions, typed review and local case behavior.
- **Excluded:** Corpus upload, external OCR, Box custody, cloud performance and unseen future formats.

### Implementation checklist

- [ ] Freeze hashed cohort/holdout and human-reviewed expected values without modifying corpus.
- [ ] Exercise the actual local caller and persisted operator result with every external adapter disabled.
- [ ] Report aggregate accuracy, missing/conflicting values, unreadable material and false creation separately.
- [ ] Preserve the untouched holdout from implementer and material test author.

### Validation checklist

- [ ] Include literal positive, staff-forwarded, contradictory, incomplete and unsupported genuine shapes where available.
- [ ] Require zero false case creations and zero silent truncation of identity-critical data.
- [ ] Confirm current legacy DOC/MSG remain retained in `Needs sorting` without reference allocation.
- [ ] Record exact command, exit, cohort/holdout hashes, input counts, caller, database provider and disabled external boundaries.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Frozen cohort through Web caller | Persisted review outcome matches approved expectations or exposes explicit difference | Caller/evaluation record | Untouched holdout or production accuracy |
| Untouched holdout | Zero false case creation and no silent identity truncation; aggregate results reported | Independent holdout report | Future layouts or cloud reliability |
| Unsupported/partial source | Source remains reviewable and no reference is allocated | Negative caller evidence | Later converter capability |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** No external approval when all calls are disabled; corpus remains local-only and immutable.
- **Rollout/activation:** Freeze evidence before implementation iteration; run holdout only after cohort behavior is fixed.
- **Rollback/recovery:** Delete only generated ignored evaluation outputs after exact-path verification; never alter corpus or application data outside the test store.
- **Irreversible risk:** Revealing holdout expectations to the implementer invalidates independence and requires a new holdout.

### Deferred-capability impact

- **Named capabilities:** Legacy DOC/MSG automation, VRM OCR/VLM, AI/vision and new principal formats.
- **Stable seam retained:** Immutable source identity, occurrence provenance, engine-neutral reader and explicit unsupported outcomes.
- **Future migration/replacement:** Later adapters need their own frozen cohort, contract and caller-backed evaluation.
- **Activation boundary:** Operator-reviewed evidence, zero false creation, cost/licence/security decision and explicit scope approval.
- **Deliberately absent:** No model, vendor call, dormant parser, uploaded dataset or widened OCR route.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Evaluation boundary and criteria exist | Accuracy, caller result and acceptance |

## Prove approved integrations in shared development

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** Each external boundary must be proven through its caller with least privilege and exact approved scope.
- **Confirmed facts:** Corpus evidence and external protocol/custody evidence are different proof classes.
- **Decision required before implementation:** Exact account, environment, mailbox/folder, Box descendant/action, vendor fixture, data scope and spending cap for every live check.

### Owner and dependencies

- **Policy/implementation owner:** Each adapter owner supplies a caller-backed smoke; release owner controls activation order.
- **Independent evaluator:** Reviewer verifies negative scope locally before any live request and challenges claims beyond the exact target.
- **Prerequisites:** Deployed shared-development Web/Worker and approved adapter tasks.
- **Consumers/unlocks:** Full operator journey and production approval.

### Caller, contract and change boundary

- **Real or intended caller:** Deployed Web/Worker/API/MCP entry points, never a standalone SDK script as final proof.
- **Input/output:** Approved non-corpus fixtures exercise identity, idempotency, permission, storage and failure mechanics.
- **Ordered decisions and failure behavior:** Prove local out-of-scope denial, approve exact target, perform one scoped call, reconcile stored identity/result, and disable the integration again.
- **Persistence/migration:** External IDs, versions, attempts and outcomes persist without secret values or source bytes in logs.
- **Adapters/side effects:** Box is confined to root `401774594028`; Graph uses Exchange Application RBAC as the sole application mail grant for one approved environment-specific mailbox/Inbox pair, with `instructions@` Inbox reserved for production; other vendors use named non-production/test contracts.
- **Operator surface and observability:** Success and typed failure are visible with correlation and no content leakage.
- **Documentation affected:** Dated integration evidence records scope and limitations; it does not broaden future permission.
- **Replaces/consolidates:** Registration or direct SDK smoke alone is not completion evidence.

### Scope

- **Included:** Approved protocol, identity, idempotency, negative-scope and caller mechanics in shared development.
- **Excluded:** Corpus transfer, live production folders/mailboxes, broad account discovery and business-accuracy claims.

### Implementation checklist

- [ ] Define an exact non-corpus fixture and external target for each integration proof.
- [ ] Demonstrate scope-guard failure with zero client calls before approving a live request.
- [ ] Exercise the actual deployed caller and reconcile durable IDs/outcomes.
- [ ] Disable the integration after evidence and retain a safe retry/recovery path.

### Validation checklist

- [ ] Prove positive and negative RBAC/credential boundaries without probing unapproved live objects, including absence of an unscoped Entra Graph application mail grant and denial for a second mailbox/non-Inbox folder.
- [ ] Prove replay/duplicate handling and permanent/transient/unknown failure mapping.
- [ ] Record account, region/environment, target IDs, action, input class, cost cap, exit and skipped scope.
- [ ] Confirm no corpus bytes or secrets appear in request, logs or artifacts.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Locally rejected out-of-scope target | Typed violation and zero external client calls | Mock/spy negative test | Live provider permissions |
| Exact approved target through deployed caller | One durable, correlated result with expected external identity | Scoped smoke record | Any other folder/mailbox/account or business accuracy |
| Replay/transient failure | No duplicate business action; bounded retry or visible terminal result | Caller and persistence evidence | Sustained production reliability |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Every live request needs exact account/identity, target/action, fixture/data, environment and cost approval.
- **Rollout/activation:** Enable one integration for one smoke, capture evidence, then disable until the next approved journey.
- **Rollback/recovery:** Disable configuration, retain durable receipt/outcome and reconcile without deleting source or case data.
- **Irreversible risk:** Any permission broadening, data transfer or production-object mutation requires a new decision.

### Deferred-capability impact

- **Named capabilities:** Broader mailboxes, production Box, EVA API, outbound messages and malware scanning.
- **Stable seam retained:** Channel/external IDs, narrow adapters, durable attempts and explicit activation configuration.
- **Future migration/replacement:** Each later scope needs new permission, data-flow, negative tests and operational approval.
- **Activation boundary:** Named business scope, licence/consent, security evidence and direct approval.
- **Deliberately absent:** No account-wide search, enterprise event feed, outbound sender, scanner or production allowlist.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Integration proof is scoped | External call, deployment and acceptance |

## Complete operator acceptance and production cutover

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** Alex and relevant staff perform technical/operational acceptance; management approves production release.
- **Confirmed facts:** Production cannot be accepted while required task evidence or affected canonical decisions remain open.
- **Decision required before implementation:** Close the [open decisions](../../open-decisions.md) before exercising their lifecycle/report steps; approve production Box roots/actions separately because the current root is proof-only.

### Owner and dependencies

- **Policy/implementation owner:** Release owner coordinates; each feature owner remains accountable for its task evidence.
- **Independent evaluator:** Final reviewer differs from implementation and material test author; operators judge the observed workflow.
- **Prerequisites:** All required areas reach caller-backed local/shared-development evidence and release rollback is proven.
- **Consumers/unlocks:** Management production approval and controlled go-live.

### Caller, contract and change boundary

- **Real or intended caller:** Authenticated operator Web journeys plus delivered Worker/provider/MCP entry points required by first-release scope.
- **Input/output:** Approved real-shaped non-corpus input and staff actions yield a complete, auditable QDOS case journey and recoverable release.
- **Ordered decisions and failure behavior:** Verify roles/principal configuration, intake/custody, case acceptance/reference, exclusive case editing, manual WhatsApp material, chasing/review, EVA hand-off, report/post-report and terminal/reopen behavior as settled; stop on unresolved authority or failed evidence.
- **Persistence/migration:** Acceptance names environment, database/migration, artifact and retained audit/file identities.
- **Adapters/side effects:** Enable only separately approved external scopes and one production poller.
- **Operator surface and observability:** Every queue, count, search, active-editor/read-only state, stale/failure state, keyboard path, audit and alert used in the journey is observed.
- **Documentation affected:** Record acceptance corrections separately from technical verification; accepted decisions update canonical sources.
- **Replaces/consolidates:** No checklist signature can substitute for observed operator behavior.

### Scope

- **Included:** Complete first-release QDOS workflow, role/security checks, accessibility, recovery and one-at-a-time cutover.
- **Excluded:** Deferred features, unapproved external scope, predecessor retirement and claims of broader reliability than observed.

### Implementation checklist

- [ ] Freeze the accepted journey, actors, input class, expected outputs, environment and external scopes.
- [ ] Run role-specific operator journeys and record corrections separately from technical failures.
- [ ] Obtain management approval only after scoped technical, recovery and rollback evidence is complete.
- [ ] Release outside office hours and enable each approved integration only after its negative and smoke checks.

### Validation checklist

- [ ] Exercise all agreed case types, inbox/case states, reference forms, matching, search, review gates, terminals and settled reopen/report behavior.
- [ ] Use two authenticated browser sessions to prove one active case editor, read-only visibility for the second staff member, lease expiry recovery and stale-save refusal.
- [ ] Add manually received WhatsApp material through the actual case caller and verify source provenance/custody state without any WhatsApp integration or corpus upload.
- [ ] Use keyboard-only operation, semantic labels, text-plus-colour states, AA contrast and 200% zoom/reflow evidence.
- [ ] Prove health, alert, claim pause, prior-artifact rollback and exactly one poller.
- [ ] Record skipped deferred behavior and every external scope not enabled.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations immediately before release.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Named operator journey | Staff complete the QDOS workflow with correct data, files, audit and visible failures | Signed acceptance record and browser/caller evidence | Deferred features or untested roles/states |
| Two staff open the same case | One may edit; the other remains read-only until release/expiry, and stale data cannot overwrite | Two-browser, SQL and operator evidence | Collaborative editing |
| Release/rollback rehearsal | Named artifacts deploy, smoke and can return to prior artifact without down-migration | Shared-development release evidence | Production traffic or data recovery from every failure |
| Production cutover | Exactly approved integrations activate singly after health/smoke; one poller runs | Production deployment/live verification record | Ongoing business acceptance or wider external access |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Management acceptance, production deployment, secrets, Exchange RBAC, provider credentials, MCP/Claude processing, mailbox cutover and production Box each require separate approval.
- **Rollout/activation:** Apply migration, deploy Web then Worker, pass smoke, enable integrations individually, observe, then remove the write gate.
- **Rollback/recovery:** Reapply gate/pause, disable integrations, redeploy prior artifacts and restore to a new database only if required.
- **Irreversible risk:** Predecessor retirement or deletion is outside this cutover and requires a new exact-target operation.

### Deferred-capability impact

- **Named capabilities:** Every named deferral in the questionnaire and remaining-requirements plan.
- **Stable seam retained:** Accepted Core use cases, source/external identities, adapter boundaries, audit and immutable artifacts.
- **Future migration/replacement:** Each later capability owns its schema/contract/adapter and evidence without rewriting accepted history.
- **Activation boundary:** New product decision, genuine evidence, licence/cost/security review and explicit approval.
- **Deliberately absent:** Deferred code, resource, integration, account, scanner, network and release machinery remain absent.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Acceptance/cutover sequence exists | Operator acceptance, deployment and live verification |
