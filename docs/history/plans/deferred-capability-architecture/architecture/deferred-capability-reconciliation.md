# Deferred-capability architecture reconciliation

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Historical/reference reconciliation evidence — not the current activation route.** Canonical current routing is the [deferred-capability activation index](../README.md), the [later-delivery index](../../later-delivery/README.md), the [feature maturity map](../../feature-maturity-map.md), and the accepted [architecture decisions](../../../../architecture/decisions/). This retained file cannot allocate a feature, amend an ADR, or create implementation work.

## Purpose

Refine this saved plan against the user-authorised reference review so named deferred capabilities are considered from the start without being implemented early. The observable outcome of this slice is a review-complete set of findings and separately approvable amendment proposals. It does not amend product authority or accepted architecture by itself.

## Authority and current boundary

- **Current user instruction:** factor deferred items into architecture from the start; a deferral does not mean ignore the item. For this slice, compare the requested evidence one source area at a time with this saved plan and add missing features or clarity to the plan pack.
- **Authority order:** authoritative [operator requirements](../../../../operator-notes/product-requirements/required-capabilities.md), settled [discovery-questionnaire](../../../product/project-discovery-questionnaire.md) answers, and accepted [architecture decisions](../../../../architecture/decisions/) retain their repository-defined authority. [Remaining requirements](../../../../product/qdos-alpha-gap.md) is planning evidence and [open decisions](../../../../product/open-decisions.md) is an ambiguity register; neither is promoted into product authority by this plan.
- **Reference evidence boundary:** the user-authorised review of the [EVA API material](../../../../reference/EVA/EVA_API_SCHEMA.md), [EVA screenshots and notes](../../../../reference/eva_information/eva_screenshot_findings.md), [reference reports](../../../../reference/reports/), and [current email tree](../../../../reference/CollisionSPikeCurrenttree.txt) supplies candidate shapes, contradictions, and migration risks only. Reconcile every supported finding against the authority order; do not promote a report, screenshot, example payload, predecessor contract, or its `accepted` label wholesale.
- **Policy owner:** the project/product owner owns product classification; the existing ADR owners own architecture compatibility decisions.
- **Current implementation:** The [source-of-truth order](../../../../agent-guidance/source-of-truth.md), accepted [architecture overview](../../../../architecture/README.md), and [maturity map](../../feature-maturity-map.md) define the current contract and allocation. The only evidenced runtime intake caller is the Development-only [`/Intake/Upload` page](../../../../../src/Pegasus.Web/Pages/Intake/Upload.cshtml.cs), which calls [`ProcessIntake`](../../../../../src/Pegasus.Core/Intake/ProcessIntake.cs) with the registered [`QdosInstructionExtractionPolicy`](../../../../../src/Pegasus.Core/Intake/QdosInstructionExtractionPolicy.cs). The Worker has no intake trigger.
- **Real callers:** architecture authors, plan authors, implementers, and reviewers consume this documentation outcome. The runtime inventory above is an evidence limit, not proof of mailbox, Worker, provider-API, Box, EVA, or deferred-capability callers. Every later feature must independently identify and prove its production caller.
- **Persistence/adapters:** none are changed. The plan may describe stable concepts and existing boundaries but cannot create a table, migration, port, adapter, queue, endpoint, configuration key, feature flag, or Azure resource.
- **Dependencies:** the source-of-truth order and canonical ambiguity register. A missing material business rule withholds only its affected slice.
- **Replaces/consolidates:** this temporary review consolidates reference findings and proposed dispositions only. Any later canonical amendment is separately scoped and approved; this plan must not become a permanent parallel register.

## Shared failure and observability rules

The primary failures are classification drift, presenting planned architecture as implemented behavior, silently foreclosing a named capability, and adding dormant scaffolding to appear future-ready. Each failure remains visible through clause-level parity review, explicit evidence-state language, scoped Markdown validation, and independent review. No runtime telemetry or action-history event is applicable to this documentation-only change.

## Reconcile the authoritative classifications and accepted ADRs

**Underlying canonical-reconciliation state:** Planned. This plan-refinement slice is recorded separately under completion evidence.

### Authority and decision gate

- **Requirement/decision:** the user requires deferred capabilities to be considered from the start. The repository contract defines compatibility as non-foreclosure, a stable current concept or an explicit future migration, an activation gate, and a statement of what is not built now—not speculative implementation. Every plan, design, schema/API change, and architecture decision needs a relevant `Deferred-capability impact` section.
- **Confirmed facts:** current blockers, allocated later work, conditional `Later`/`unallocated` items, `Not planned` boundaries, vendor options, and ADR-local alternatives are distinct. The maturity map supplies the schedule; reference evidence cannot change it.
- **Decision required before implementation:** no decision is required to refine this plan. The sole material product research decision is combined mailbox categorisation and all automatic email matching. Image association keeps the settled conservative boundary, QDOS address conflicts remain reviewable without an inferred precedence rule, unlisted email operations stay unsupported/deferred, and the focused-`0.1.0-alpha.1` EVA mapping is reversible integration-contract work requiring operator acceptance. If later evidence exposes a genuinely new material business ambiguity, obtain a direct user decision before adding it to [open decisions](../../../../product/open-decisions.md); do not manufacture another blocker here.

### Owner and dependencies

- **Policy/implementation owner:** project/product owner for any later questionnaire proposal; existing ADR owners for any later amendment proposal; one documentation implementation owner for this bounded plan refinement.
- **Independent evaluator:** a reviewer who did not author the amendments checks authority precedence, complete parity, caller reality, and absence of speculative components.
- **Prerequisites:** re-read current operator notes, questionnaire, planning evidence, ambiguity register, accepted ADRs, the authorised reference-review findings, and current callers immediately before editing.
- **Consumers/unlocks:** future architecture, schema, API, integration, and implementation plans.

### Caller, contract and change boundary

- **Real or intended caller:** architecture and delivery-plan authors consume the documentation contract; no product endpoint is changed or proved.
- **Input/output:** current authority and accepted decisions produce temporary clause-level review evidence, the findings in this plan, and separately reviewable amendment proposals. Reference-only findings are recorded as rejected, already covered, or withheld rather than promoted into an evergreen register. No questionnaire, ambiguity-register, requirements-baseline, or ADR amendment is part of this slice.
- **Ordered decisions and failure behavior:** classify authority first; distinguish the combined research decision, current implementation design, allocated later work, conditional `Later`/`unallocated`, and `Not planned`; keep ADR-local alternatives with their owner; stop on foreclosure, unsupported product rules, or authority changes.
- **Persistence/migration:** not applicable because no runtime data changes. Any later ADR proposal names only the future migration implied by that ADR's own decision.
- **Concurrency/edit ownership:** use one documentation owner and re-read each target before patching so concurrent user edits are preserved. Resolve overlaps by narrowing the patch; never restore, reset, stash, or overwrite unrelated work.
- **Adapters/side effects:** none. No external client or cloud operation is allowed.
- **Permission/scope guard:** repository-local Markdown files only; operator notes, corpus, Azure, credentials, and external systems remain read-only or untouched.
- **Operator surface and observability:** no operator UI change. The visible result is the reviewed Markdown diff and parity evidence.
- **Documentation affected in this slice:** this plan index and area plan only. A later, separately approved task may propose narrowly scoped changes to the questionnaire, ambiguity register, remaining-requirements baseline, or an accepted ADR. `AGENTS.md`, operator notes, source-of-truth guidance, runtime code, and external systems remain unchanged.
- **Replaces/consolidates:** identify weak or undifferentiated `later` wording for separately approved correction; do not add a second deferred-feature registry or a new umbrella ADR.

### Scope

- **Included:** refinement of these two plan documents; evidence-faithful disposition of the authorised reference findings; proposed current-scope clarifications, non-foreclosure questions, activation gates, negative boundaries, scoped validation, and independent review.
- **Excluded:** edits to the questionnaire, ambiguity register, remaining-requirements baseline, ADRs, runtime APIs or types, database schema, migrations, code, projects, adapters, dependencies, queues, endpoints, flags, configuration, infrastructure, deployment, operator notes, corpus, and activation of any deferred capability.

### Evidence-faithful disposition used by this review

This table is a temporary review aid, not a replacement for the questionnaire and not an accepted roadmap. Preserve the source's exact wording when preparing any later amendment proposal.

| Review disposition | Capabilities or questions |
|---|---|
| Sole material open product research decision | Combined mailbox categorisation and all automatic email-matching policy, including automatic intake/correspondence/report/Triage candidate and match predicates, governance, ambiguity, correction and retained evidence |
| Required current implementation/contract design; not a product ambiguity | Conservative image association with uncertainty in `Needs sorting`; confirmed physical inspection address or exact `Image Based Assessment` with no inferred principal precedence; `0.1.0-alpha.1` inbound receipt plus research-gated exact report/Triage matchers and the reasoned report fallback; focused-`0.1.0-alpha.1` reversible EVA JSON/image mapping, selection, readiness, revision and recovery subject to operator contract acceptance |
| Allocated later or conditional work | `Next`/`unallocated` four-mailbox/email/provider/OCR/DOC/MSG/post-report/image-assistance work; `Later`/`unallocated` Diminution, Commercial, WhatsApp, chasers, assistant and conditional AI work; `Later`/`unallocated` EVA/finance/report/AI-Assessor work. Guided capture/vendor capture/custom domain remain conditional `Later`/`unallocated` |
| Potential vendor option under evaluation; not selected | Tractable/Ravin guided-capture services |
| ADR-local technical alternative; not a product classification | Graph polling to webhooks; PDF-engine replacement; modular-monolith component extraction; generic capacity or SKU upgrades beyond the specifically authorised operational options |
| Permanent predecessor exclusion | Migration of previous CollisionSpike application cases or state; keeping the previous CollisionSpike application available after the validated `Next`/`unallocated` cutover; reuse of predecessor application code |
| Permanent `Not planned` boundary | SMS, Teams, portal/external accounts; redaction, signatures, malware scanning, retention/deletion, legal hold, subject-request and DPIA workflows; separate QA/UAT/staging/demo/training environments; GitHub Actions/OIDC; S1/slots; private networking; zone/multi-region resilience; quarterly recovery exercises |

These four current-scope implementation/contract boundaries do not create four product ambiguities. Automatic email candidate and match predicates stay in the sole combined research decision, with `0.1.0-alpha.1` exact report/Triage matchers and `Next`/`unallocated` general association retaining their separate allocations. Image association remains conservative when evidence is not definitive, with a shared vehicle registration alone insufficient; tests must prove uncertainty remains visible. QDOS accepts a confirmed physical address or exact `Image Based Assessment` without an inferred precedence rule, so contradictions remain reviewable. Unlisted email operations are unsupported unless directly allocated; general in-app compose/reply/forward/send is `Not planned`. Exact EVA field/image mapping and other reversible integration-contract choices stay in the owning integration plan until operator acceptance.

Settled cross-cutting rules are no longer classified as blocked: allocated case principal/reference are immediately immutable; wrong-principal work makes the original terminal `Created in error` and links a newly allocated replacement; a used principal code is replaced through a linked-principal cutover transaction rather than edited; the permanent action-history boundary is defined; there is no pre-send report review gate; report evidence is an exact Sent item from the approved-mailbox allowlist while the `0.1.0-alpha.1` automatic matcher remains research-gated; `Triage` has settled states, findings, exact reply evidence, correction/reopen/linking and permissions; chase/Held/reopen and London dashboard boundaries are fixed; and the stable `Sent to Engineer` event uses first successful EVA JSON/image export generation as an explicitly limited `0.1.0-alpha.1` proxy.

Named deferrals, potential vendor options, and ADR-local alternatives remain prohibitions for current delivery unless their own source-specific gate and a fresh direct decision authorise implementation. Recording non-foreclosure does not add a schedule or strengthen a source's commitment. The previous-CollisionSpike migration exclusion does not decide whether a later EVA replacement requires active-data migration, backfill, coexistence, or cutover.

### Required-now implementation and contract boundaries

| Boundary | Settled current rule | Implementation/contract work before activation | Owning follow-up |
|---|---|---|---|
| Associations | Automatic email matching belongs only to the combined research. Image association stays conservative: vehicle registration alone is insufficient, uncertainty remains in `Needs sorting`, and staff can link/reverse with permanent action history | The Core association contract defines definitive evidence combinations, contradictions, ties, closed/reopened candidate treatment and duplicate effects as implementation design, then proves them with caller-backed cohorts. Unsupported combinations remain manual/`Needs sorting` | Association owner and acceptance cohort; route only automatic email candidate/match predicates to the combined research |
| QDOS inspection-address mode | Accept a confirmed physical vehicle/repairer address or exact `Image Based Assessment`. Do not infer a QDOS/principal precedence or hard-code a field matrix; missing/contradictory input remains reviewable | Implement typed confirmed-value/provenance handling and explicit unsupported/contradictory outcomes. Any future Administrator setting or override would need its own role/action-history contract | Intake/case-field owner and caller tests; no product-decision register entry |
| `0.1.0-alpha.1` email boundary | Durable staff-forwarded `instructions@` ingestion, application-owned association/history, Box storage, the approved-mailbox allowlist, automatic exact report matching with reasoned exact-item fallback, and exact no-fallback Triage reply matching are required. `Next`/`unallocated` owns four-mailbox management and supported folder mutations; general compose/reply/forward/send is `Not planned` | Implement only the `0.1.0-alpha.1` named read/evidence operations with roles, action history, retries and correction after the combined research accepts their predicates. Preserve `Next`/`unallocated` source/message identities without adding `Next`/`unallocated` actions, and add no in-app sending path | Outlook plan/adapter; combined research for `0.1.0-alpha.1` exact predicates and `Next`/`unallocated` expansion |
| Focused-`0.1.0-alpha.1` EVA manual handoff | Operator-approved structured JSON plus stored images are manually transferred to EVA; EVA remains authoritative for assignment, estimating, valuation and report generation; pre-assignment review and visible export failure are required. First successful generation records the stable first-Sent-to-Engineer proxy once, but does not prove receipt | Define reversible versioned JSON mapping, null behavior, image selection/naming/order/manifest, generation/download readiness, import confirmation, revision/retry and reconciliation and obtain operator contract acceptance | Owning EVA integration contract and operator acceptance, not the product-decision register; later replacement changes evidence source while retaining the stable event |

### Non-authorising reference-review families

The following rows retain missing clarities found during the requested source-by-source review. They are temporary review families, not canonical product classifications, independently activatable units, accepted current architecture, or content to copy wholesale into ADR-0002. A row may group related questions solely to keep the review readable. Before any authoritative proposal or implementation, decompose it by independently authorised caller, permission, external side effect, data authority, rollout, and rollback.

| Review family and source wording | Accepted current-scope fact and evidence state | Later questions or migration to preserve | Do not infer or build now |
|---|---|---|---|
| Additional Outlook mailbox ingestion — `Next`/`unallocated` | **Allocated; not called:** `0.1.0-alpha.1` uses staff-forwarded `instructions@`; `Next`/`unallocated` adds `desk@`, `engineers@`, and `info@` through the same owner | Approve each mailbox/folder/RBAC scope and prove cursor, recovery, classification, association, and acceptance | No broad permission or mailbox-specific policy |
| Broader in-app email management — `Next`/`unallocated` with permanent compose/send boundary | **Allocated; not called:** `Next`/`unallocated` provides browse/search/threading, suggested folder moves, and MAIL-13 mutations. General compose/reply/forward/send remains `Not planned`; automatic chasers/reports are separate `Later`/`unallocated` capabilities | Define exact mailbox/folder permissions, side effects, retries, correction, rollout, and acceptance | No second classifier or general mail client inferred from the classification taxonomy |
| Automated WhatsApp ingestion/automation — `Later`/`unallocated` | **Allocated; not called:** `0.1.0-alpha.1` remains manual | Decompose inbound/outbound scopes, identity, permissions, retention, matching, retry, and vendor approval | No dormant WhatsApp adapter in `0.1.0-alpha.1` |
| Automated outbound chasers — `Later`/`unallocated` | **Allocated; not called:** `0.1.0-alpha.1` retains seven-day reminders and copyable content | Define approval, send, retry, recipients, delivery semantics, recovery, and history | Preparing/copying is never evidence of sending |
| Direct EVA API use — conditional `Later`/`unallocated` | **Allocated conditionally; not called:** `0.1.0-alpha.1` uses JSON/images and EVA remains authoritative | Activate only when the EVA vendor capability is usable and the operation-specific contract/security evidence is accepted | Legacy endpoints and examples are evidence only |
| Staged replacement of EVA functions — named future direction, separate from API use | **Current external authority:** EVA owns Engineer assignment, estimating, valuation, and report generation. Preserve stable local case/reference, named case relationships, Box custody, evidence, and accepted downstream IDs | Decompose assignment, estimating, valuation, report generation, status/correspondence, and any other approved function into separate authority-transfer slices with coexistence, reconciliation, migration/backfill, cutover, rollback, and retirement | No big-bang replacement, duplicate EVA model, dormant workflow, or inferred adoption of every EVA screen. Previous-CollisionSpike migration exclusions do not decide EVA cutover |
| Repair-estimate and estimating-service workflows — `Later`/`unallocated` | **Allocated; not called:** preserve the distinction from current findings/report evidence | Select authority/provider and define identity, currency, lines, versions, approvals, evidence, correction, and migration | No dormant estimate model/client |
| Vehicle-valuation workflows and integrations — `Later`/`unallocated` | **Allocated; not called:** valuation remains distinct from estimate and invoice | Select authority/providers and define values, dates, versions, approvals, evidence, correction, and migration | No dormant valuation model/client |
| Invoice and accounting workflows/integration — `Later`/`unallocated` | **Allocated; not called:** invoice remains distinct from estimate/valuation | Select authority/provider and define invoice/payment identity, lifecycle, tax, approval, reconciliation, and migration | No dormant ledger/client |
| Diminution cases — `Later`/`unallocated` | **Allocated; unsupported now:** retain shared identity/sequence invariants until its own rules are approved | Obtain operator-approved fields, workflow, references, Box/EVA behavior, outcomes, rollout, and reversal | No guessed `D.` marker or independent counter |
| Commercial cases — `Later`/`unallocated` | **Allocated; unsupported now:** retain shared identity/sequence invariants until its own rules are approved | Obtain operator-approved fields, workflow, references, Box/EVA behavior, outcomes, rollout, and reversal | No guessed `C.` marker or independent counter |
| Collision Engineers guided mobile capture — Unclear | **Unassigned:** preserve source/asset identity only | Requires a later direct product decision and full security/operations contract | No dormant portal/mobile route |
| Tractable/Ravin guided capture — Unclear | **Unassigned vendor option** | Requires later selection, licence/security decision, and accepted scopes | No vendor SDK/client/configuration |
| AI and image/vision assistance — `Next`/`unallocated` and `Later`/`unallocated` by feature row | **Allocated, not called:** AI-05 is `Next`/`unallocated`; AI-01 is `Later`/`unallocated`; AI-02/03/04/06 are conditional `Later`/`unallocated`; AI-07 is staff-selected `Later`/`unallocated` after EVA replacement | Treat every task separately with typed output, model/version provenance, uncertainty, staff disposition, evaluation, cost/licence/data approval, rollout, and rollback | No general AI service or automatic business decision inferred from the family name |
| Automatic vehicle-registration reading — `0.1.0-alpha.1` | **Allocated; not called:** originals and suggestions remain reviewable | Select and prove the mechanism against representative labelled evidence; do not infer OCR/VLM or merge with `Next`/`unallocated` image AI | No unproved recogniser or automatic acceptance |
| Automated legacy DOC/MSG extraction — `Next`/`unallocated` | **Allocated; not called:** `0.1.0-alpha.1` retains originals in `Needs sorting` | Validate bounded readers through the existing contract and real caller | No dormant converter in `0.1.0-alpha.1` |
| Inspection-address mapping/prediction — `0.1.0-alpha.1`; AI suggestions conditional `Later`/`unallocated` | **Allocated distinctions:** accepted case value remains staff-confirmed | Define deterministic `0.1.0-alpha.1` mapping separately from any conditional AI source | No inferred principal default or automatic overwrite |
| Automated malware scanning — Never | **Permanent boundary:** inputs remain untrusted and must not be labelled safe | No activation plan under current product decision | No scanner/quarantine implementation |
| External/customer accounts — Never | **Permanent boundary:** provider machine clients and staff MCP are not external user accounts | No activation plan under current product decision | No external role/tenant/invitation flow |
| Custom Collision Engineers domain — Unclear | **Unassigned** | Requires a later direct decision plus DNS/TLS/OAuth rollout contract | No dormant domain configuration |
| Multi-region failover — Never | **Permanent boundary:** UK South single-region | No activation plan under current product decision | No second region/failover topology |
| Zone redundancy — Never | **Permanent boundary** | No activation plan under current product decision | No zone topology |
| Private networking — Never | **Permanent boundary** | No activation plan under current product decision | No VNet/private endpoints |
| Separate staging environment — Never | **Permanent boundary:** local, shared development/integration, and production only | No activation plan under current product decision | No staging resources |
| Production deployment slots and Standard S1 — Never | **Permanent boundary:** production remains B1 direct release | No activation plan under current product decision | No S1/slot machinery |

### ADR-local technical alternatives

These choices remain with their accepted architecture owner and must not appear as product capabilities in the questionnaire or in a global deferred-feature catalogue.

| Alternative | Owning decision | Current accepted choice | Reconsideration trigger and absent implementation |
|---|---|---|---|
| Microsoft Graph webhooks | ADR-0002 Outlook ingestion | Approximate one-minute Graph delta polling is the accepted design for the authorised `0.1.0-alpha.1` Inbox; no mailbox production caller is currently evidenced | Reconsider only when measured mailbox volume, latency, or polling reliability is unsuitable. No webhook subscription, callback, validation token path, public endpoint, or parallel receipt owner exists now |
| PDF-engine replacement | ADR-0001 hybrid extraction and ADR-0003 first-QDOS engine choice | Embedded-text-first hybrid processing with the accepted PdfPig slice | Require frozen-cohort/holdout contract parity, security, licence, maintenance, performance, and real-caller evidence. No parallel reader, dormant selector, or engine feature flag |
| Modular-monolith component extraction | ADR-0002 repository/runtime boundary | Four approved production projects with feature folders; the Development-only Web upload is the sole evidenced intake caller and Worker has no intake trigger | Require measured scale, dependency, deployment, or ownership evidence plus a new ADR. No fifth project, service, network contract, queue, or duplicate policy owner |
| Generic capacity/SKU upgrades | ADR-0002 service-specific capacity decisions | Current accepted `0.1.0-alpha.1` tiers and measured-resource boundaries | Require the owning service's metrics, quota, cold-start, latency, cost, or reliability evidence. No pre-provisioned upgrade, always-ready instance, unused capacity configuration, or release gate |

### Reference findings not to encode as requirements

- Reject predecessor independent per-marker sequences and `D.`/`C.` markers because current authority requires one shared principal/year sequence.
- Withhold Experian/adverse-history checks, video screenshot extraction, fixed EVA photo ordering/reflection rules, salvage/settlement, detailed parts, engineer payments, letters, supplementary reports, rich EVA fields, and a historical-search aid until an operator decision promotes a bounded capability.
- Treat the current email tree as candidate message-purpose evidence only. Do not adopt its mirrored received/sent/reply taxonomy, mixed case-type categories, `new-client`, `website-enquiry`, or message-driven workflow transitions.
- Do not adopt the reference EVA endpoint set, five-minute token behavior, identifiers, fields, value sets, example payload, full-list report polling, or batching guidance until current vendor/tenant behavior is verified and a contract is accepted.
- Do not use a reference report's data-processing wording to widen repository/corpus egress permission or replace channel occurrence identity with a content hash.

### Proposed decision-local ADR allocation

These are review findings for later, separately approved amendments. They do not change an accepted ADR in this slice.

| Decision | Required deferred-capability treatment |
|---|---|
| ADR-0002 | Record only the cross-cutting constraints it owns: four-project modular-monolith direction, one Core owner per business policy, data/file authority boundaries, external identity preservation, composition-root responsibilities, Azure/runtime boundaries, and the rule against dormant scaffolding. Link canonical product scope and ambiguity rather than copying this review catalogue. State that previous-CollisionSpike migration exclusions do not decide any future EVA migration/cutover |
| ADR-0001 | Record how AI/vision, automated VRM OCR, and generative extraction remain reviewable proposals behind original-source authority, deterministic policy, version/confidence provenance, and staff correction |
| ADR-0003 | Keep PDF-engine replacement conditional on contract parity, frozen cohort/holdout, security, licence, maintenance, and real-caller evidence; prohibit a parallel reader or dormant switch |
| ADR-0004 | Preserve the separation between staff identity, provider machine clients, and staff MCP. External accounts require a new tenancy/data-isolation decision. A custom domain preserves the authentication model but still requires DNS/TLS and OAuth issuer/resource/callback migration |
| ADR-0005 | Preserve one Core-owned intake path and asset/source provenance for future channels, guided capture, DOC/MSG parsing, VRM vision, and scanning. Re-inventory the actual caller and source-channel implementation before amending evidence statements |
| Draft ADR-0006, if separately accepted | Preserve `ProcessIntake` as the single provider-neutral intake owner and QDOS as the sole current extraction policy. Reconcile its decision-local deferred impacts without adding a provider registry, second policy, mailbox classifier, dormant transport, or duplicate catalogue |
| Any ADR accepted after this plan review | Add only impacts created by that decision; link canonical product scope and relevant cross-cutting ADR constraints without copying a global catalogue |

For any later approved amendment, preserve the ADR's original date and status and add an amendment date and reason so new text is not presented as part of the original decision.

### Implementation checklist

- [ ] Build clause-level parity evidence and classify each item as combined research, current implementation design, allocated horizon, conditional `Later`/`unallocated`, `Not planned`, vendor option, ADR-local alternative, or rejected reference finding.
- [ ] Keep image association conservative, QDOS address contradictions reviewable, and unlisted email operations unsupported/deferred. Put only automatic email predicates in the combined research package. Keep reversible EVA field/image mapping in its integration contract and require operator acceptance without inventing another ambiguity-register entry.
- [ ] If source wording is genuinely inconsistent, prepare the smallest questionnaire proposal without rewriting operator rules or assigning a stronger roadmap status.
- [ ] Prepare a narrowly scoped remaining-requirements proposal only where its planning wording omits the repository-wide compatibility contract or contradicts authority.
- [ ] Prepare an ADR-0002 proposal containing only its owned cross-cutting invariants and canonical links; do not copy the classification table or review families into it.
- [ ] Prepare concise, decision-local `Deferred-capability impact` proposals only for accepted ADRs whose choices constrain a named future capability; do not add boilerplate to unrelated decisions.
- [ ] Record rejected and withheld reference findings only in ignored reconciliation evidence; do not turn the reference reports, screenshots, current email tree, or example payloads into another product or architecture register.
- [ ] Verify current caller and implementation claims against the live repository, remove stale `/Intake/Qdos` and `ProcessQdosIntake` claims where superseded, and label absent or intended paths as planned.
- [ ] Remove no plan, ADR, code path, or user change; consolidate only wording that would otherwise become a second authority.
- [ ] Obtain owner approval before editing any authoritative source or treating a proposed amendment as accepted.

### Validation checklist

- [ ] Map every named item in the questionnaire, planning evidence, and relevant operator notes in ignored clause-level evidence; do not create a second canonical register.
- [ ] Confirm the proposed ADR-0002 change contains cross-cutting constraints only and each focused-ADR proposal contains decision-local impacts only.
- [ ] Challenge the sole categorisation/all-automatic-email-matching research separately from conservative image association, reviewable QDOS address contradictions, deferred email operations and reversible EVA contract design; verify none of those latter boundaries is promoted into another product open decision, and verify settled Triage remains a planned deliverable.
- [ ] Challenge scanned-PDF OCR versus deferred VRM OCR; shared sequencing versus rejected `D.`/`C.` marker counters; unsupported Diminution/Commercial creation; previous-CollisionSpike migration versus possible EVA cutover; Box custody versus downstream copies; external-account tenancy; custom-domain OAuth migration; and unscanned-file safety claims.
- [ ] Confirm message classification, prepared/copyable communication, sent-report evidence, post-report work, and terminal transitions remain distinct, while unapproved reply/thread, delivery, and EVA-report-release fields are recorded only as possible future migrations.
- [ ] Re-inventory the actual Web and Worker callers and Core owner; the current evidence is Development-only `/Intake/Upload` -> `ProcessIntake` -> QDOS policy, with no Worker intake trigger. Documentation or registration alone is not caller evidence.
- [ ] Resolve every touched relative Markdown target and fragment; report file, link, and error counts without committing a new validator.
- [ ] Run `git diff --check` and `pwsh ./scripts/Test-RepositoryStructure.ps1`.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`; report unrelated dirty-tree failures separately and do not treat a green result as product proof.
- [ ] Do not run corpus evaluation: no intake behavior or extraction accuracy changes.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Allocated later or conditional capability | Evidence retains its raw allocation, current absence, future migration/questions, activation gate, and deliberately absent implementation | Maturity parity and independent authority review | Implementation, caller, deployability, or acceptance |
| A potential vendor option | Documentation states `not selected` and names the evaluation, contract, licence, security, and product decisions required without implying roadmap commitment | Authority and reference-disposition review | The vendor will ever be selected |
| An ADR-local technical alternative | It stays out of product classification and names the owning ADR's measured trigger | ADR-local review | That the alternative is selected or needed |
| A current implementation/contract boundary | Conservative unsupported outcomes are explicit, reversible contract choices remain with their owning plan, and only automatic email predicates link to the sole combined research decision | Authority-precedence, caller-backed tests and integration-contract review | Runtime behavior, operator contract acceptance, or activation |
| A reference-only concept | It is recorded as rejected, already covered, or withheld without becoming a requirement, commitment, caller, or schema | Reference-disposition evidence and independent review | That the historical feature is wanted in `Next`/`unallocated` |
| ADR-0002 or a focused ADR proposal | ADR-0002 contains only cross-cutting constraints; a focused ADR contains only constraints created by its decision; both link canonical scope instead of copying this review | Scoped ADR review | Compatibility of unrelated capabilities or acceptance of the amendment |
| Repository validation | Markdown targets resolve and repository checks complete or expose scoped/unrelated failures honestly | Exact commands, exits, and limitations | Runtime behavior, deployment, live traffic, or operator acceptance |

### Approval, rollout and rollback

- **Current authorised scope:** refine this plan index and area plan from the requested reference review. No authoritative or runtime file is changed.
- **Later approval-triggering action:** any edit to the questionnaire, ambiguity register, remaining-requirements baseline, or accepted ADR is a separately scoped follow-up requiring its existing product/architecture owner review.
- **Rollout/activation:** review this temporary analysis, approve proposals individually, amend only the canonical owner, run scoped checks, and mark an amendment `Accepted` only through the repository's normal owner review.
- **Rollback/recovery:** continue editing or remove only this unaccepted plan refinement. A later task may revert only its own scoped canonical amendment. No runtime data or external state requires recovery.
- **Irreversible risk:** silently changing a settled product commitment or making a future capability impossible. Mitigate through source precedence, explicit amendment history, parity review, and a new ADR for any material boundary change.

### Deferred-capability impact

- **Named capabilities:** all allocated later, conditional `Later`/`unallocated`, `Not planned`, vendor, and ADR-local items above, retaining the maturity map's classification.
- **Stable seam retained:** one Core policy owner per business rule; stable principal, immutable allocated case/reference, explicit replacement-case relationship, named case relationships, source-occurrence, evidence, and external identities; source/provenance and action-history-backed association/address changes; Box folder/file/version custody; original-source provenance; versioned contracts where already required; the existing persistence migration authority; and narrow infrastructure/external adapters. This does not authorise a generic party model, party master, reference alias, deduplication rule, cardinality, or temporal schema.
- **Future migration/replacement:** each later capability still supplies its own domain rules, schema changes, caller, adapter, credentials/permissions, observability, rollout, recovery, and acceptance evidence. Where no current seam exists, the owning future plan records the migration instead of creating a dormant seam or a global ADR row.
- **Activation boundary:** use the capability's existing product decision, representative evidence, scale, licence, vendor availability, security review, architecture decision, cost acceptance, and exact external approval. If authority states no trigger, activation requires a new direct product decision and scoped ADR before implementation.
- **Deliberately absent:** no dormant project, service, queue, table, endpoint, parser, model client, scanner, external-user model, infrastructure resource, dependency, configuration, feature flag, or release gate.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| This plan-refinement slice | Locally verified and independently reviewed; no remaining material review findings | The two files in `docs/plans/deferred-capability-architecture/` | Requested reference findings, authority limits, caller reality, temporary review dispositions, missing current decisions, future migrations/gates, and negative boundaries are recorded coherently | Owner acceptance or any questionnaire, ambiguity-register, requirements-baseline, ADR, runtime, cloud, or external change |
| Scoped Markdown check | 2 files, 20 relative links, 0 link errors, 0 table errors, 0 trailing-whitespace errors | Plan index and area plan, including relative targets/fragments | Local link, table, and whitespace integrity of this untracked plan pack | Authority correctness, runtime behavior, or future link stability after other edits |
| `pwsh ./scripts/Test-RepositoryStructure.ps1` | Passed: `Repository structure is valid.` | Repository structure after the final plan wording | Current structural rules accept the saved plan location/content | Product behavior or approval |
| `git diff --check` | Exit 0 with unrelated existing LF-to-CRLF warnings | Tracked worktree diff | No tracked whitespace error was reported | The untracked target plan pack; its whitespace was checked separately above |
| `pwsh ./scripts/Invoke-RepoCheck.ps1` | Passed during this documentation slice: build 0 warnings/errors; Core 28/28, integration 82/82 excluding corpus, architecture 30/30; 5 project skills validated | Unchanged runtime and repository checks before final wording-only review corrections | The existing runtime/test baseline remained green during the plan edit | Corpus behavior, live callers, deployment, operator acceptance, or later canonical amendments |
| Underlying canonical reconciliation | Planned; not run in this slice | Separately approvable product/architecture documentation | Draft review dispositions, edit boundaries, sequence, and acceptance criteria are recorded | Owner agreement, authoritative amendments, runtime compatibility, deployment, or acceptance |
