# ADR, FRD and PRD audit register

**Baseline:** `collisionengineers/pegasus`, `dev` at `26ba4ed408317cccdb354dc1e115b0297f15df94`, 8 September 2026.

All 39 issued ADR bodies and all twelve FRDs were read. Findings below concern documented contracts and source ownership; a “retain” verdict is not a fresh implementation, vendor, deployment or acceptance certification. The current PRD is reviewed separately at the end. Use `audit.md` for priorities, migration maps and the proposed ownership model.

## 1. ADR-by-ADR register

The metadata status column reports the document's current designation. “Partly replaced” describes substantive scope and is not a proposed new status enum. Correct reciprocal metadata and the current-decision view without deleting stable IDs or rewriting the original technical rationale as though it had always said something else.

| ADR | Current designation | Verdict | Required action / surviving decision |
| --- | --- | --- | --- |
| ADR-0001 — Hybrid PDF extraction | Superseded | Keep history | ADR-0040 now owns qualified OCR. Repair active links that still present 0001 as the current selection. Its experimental/hybrid context remains evidence, not an active fallback requirement. |
| ADR-0002 — .NET modular monolith on Azure | Accepted; partly replaced | Reconcile active clauses | Retain the surviving stack, policy ownership and core topology. Its older App Service/environment/MCP/activation/cost assumptions have later owners. Map individual clauses through ADR-0004/0007/0014/0015/0024/0030/0033 and other relevant successors. Follow 0032 through 0033; a historical link is not a current-decision endpoint. |
| ADR-0003 — PdfPig for the first QDOS slice | Accepted | Retain engine choice; qualify historical slice | Its restriction on deployed manual upload is conditional on authentication and durable custody, not an eternal ban. Do not treat its old local-storage observation as proof of current production composition. Keep PdfPig ownership and route current custody behavior to FRD-02/05. Resolve obsolete benchmark/source references through exact historical revisions rather than inventing current files. |
| ADR-0004 — Provider API and staff MCP authentication | Accepted; MCP portion superseded | Prior header defect repaired | ADR-0011 replaces staff impersonation/per-staff MCP with the Automation Actor. Preserve the distinct Provider API boundary. The accepted-set view must not expose the old staff-MCP clause as active merely because the whole record is not superseded. |
| ADR-0005 — Multiformat intake assets | Accepted; partly replaced | Add clause-level successors | Its initial caller ownership changes under ADR-0006; DOC/MSG integration under ADR-0025; VRM recognition under ADR-0019; OCR under ADR-0040. Historical format deferrals and initial implementation limitations must not become current bans. The source-occurrence and evidence-preservation decisions that survive should remain clear. |
| ADR-0006 — Provider-neutral intake with contained QDOS policy | Accepted | Retain core decision; repair relationships | The body explicitly changes part of ADR-0005 but its relationship metadata does not fully represent that change. ADR-0008 and the current principal-wide FRD-09 ownership refine route selection. Retain one shared intake implementation rather than a special second QDOS engine. |
| ADR-0007 — Direct-terminal Azure deployment | Accepted; partly replaced | Header partly repaired; recovery scope remains | Environment choice is replaced by 0014 and hosting/artifact choice by 0015; workstation policy now reaches 0039 through 0037. Retain exact-SHA terminal deployment and approval boundaries. Resolve its old alpha recovery-gate implication against the current PRD's non-blocking OPS-09. Do not replay a historical environment or artifact route. |
| ADR-0008 — Separate direct-provider and intermediary policies | Accepted | Retain; complete metadata | The durable separation is still relevant. Record the relationship to the portions of 0006 it changes. Adding evidence for a principal does not require an entirely new architecture ADR per profile. Current route predicates and ambiguity behavior belong in FRD-09. |
| ADR-0009 — Adopt monorepo workspaces | Accepted; refined by 0025 | Retain admission rules, not fictitious active imports | A retired import does not invalidate the future admission boundary. ADR-0025 integrated selected slices into Infrastructure. State that there are no current independent workspace build units; preserve original import hashes and the contract for a future deliberately admitted import. |
| ADR-0010 — Single CONTEXT domain documentation | Superseded | Keep tombstone/history | Governance has moved to the documentation model and index. Do not revive this as a technical architecture requirement or delete its stable citation key. |
| ADR-0011 — Restrict MCP to Automation Actor | Accepted | Retain; prior metadata defect repaired | The successor relationship to 0004 is now recorded. Keep non-human actor identity separate from staff identity, and read current action rights through 0031/FRD-10 rather than an older static tool count. |
| ADR-0012 — Conservative MOT mileage estimation | Superseded | Keep history; repair active inbound ownership | Current behavior belongs in FRD-06. Active requirements should cite that owner; an old technical citation must not become a second calculation authority. No new mileage method is selected by this audit. |
| ADR-0013 — QDOS alpha implementation contract | Superseded | Supersession is too broad | ADR-0029 changes the image-origin boundary, not every clause of this large implementation contract. Current architecture still relies on its authentication-throttling clause. Identify each surviving decision and its active owner; either mark the actual partial scope or provide appropriate successor records. Avoid hiding live rules behind a wholly superseded badge. |
| ADR-0014 — Local-to-production deployment | Accepted | Retain; prior metadata defect repaired | Local development and production only remain the intended environment model. The successor relationship to the old deployment environment rule is now present. This decision does not by itself authorize an actual production write. |
| ADR-0015 — Container Apps Consumption Web hosting | Accepted; refined | Reconcile scale-to-zero trade-off | Retain the selected host and OCI approach; renderer packaging is refined by 0028. Its original minimum-zero/cold-start choice differs from the recorded always-warm Web deployment. Record an accepted operational override or technical successor as appropriate. ADR-0033 warms the Worker and is not automatically the Web decision. |
| ADR-0016 — Standalone desktop email evaluator | Accepted | Retain separation | The evaluator is independently owned and outside the Web caller/runtime. Its source evaluation may support evidence, but its allocation and existence must not make it a hidden QDOS/v1 acceptance gate through the capability table. |
| ADR-0018 — Provider inspection-mode database setting | Accepted | Retain persistence choice; retire old procedure deferral | Current FRD-04 includes Administrator control, whereas Decision 5 treats changing existing values as a manual SQL operation until a UI is justified. Keep the provider default and provenance rules; route current operation to the supported Administrator action. Do not let the old SQL recipe bypass history/version behavior. |
| ADR-0019 — In-process ONNX VRM recognition | Accepted | Retain engine; make automation exception explicit | The original suggestion/staff-confirmation wording must be read with the accepted 0.80 automatic registration/association exception. Keep source-image evidence, abstention and no ordinary-image egress. Do not generalize the specific accepted match permission into blanket autonomous fact confirmation. |
| ADR-0020 — Accepted QDOS association predicates | Superseded | Keep historical acceptance; fix active owners | Current behavior and principal-wide policy references belong in FRD-09. The old policy versions are historical; do not update current documents to the September 5 review's now-obsolete v4/v5 target. |
| ADR-0021 — Automation direct-write assessment contract | Superseded | Clarify what 0031 now owns | The current contract is 0031/FRD-10. Statements that the old record is wholly superseded yet “other clauses stand” create ambiguous active authority. Preserve old rationale, but identify the one current rights/tool contract. |
| ADR-0022 — Approved mailbox identity and enablement | Superseded | Keep history | ADR-0024 owns the later stable approved-mailbox/fresh-start boundary. Repair links that still cite 0022 as the complete current contract, particularly in operational configuration explanations. |
| ADR-0023 — Documentation/reference restructuring | Superseded | Keep tombstone/history | This is retired governance, not a reason to require another technical ADR for every documentation move. Current placement authority must explicitly allow the proposed procedure/skill support files. |
| ADR-0024 — Stable mailbox identity and explicit baseline | Accepted | Technical successor needed for identity reconciliation | Preserve stable ApprovedMailbox identity, activation cutoffs and fresh starts. Reconcile its immutable Graph-item occurrence/deduplication contract with FRD-08's mailbox/RFC Message-ID boundary. Specify separate identities, collisions, missing/contradictory evidence and replay behavior rather than simply renaming a field. |
| ADR-0025 — Integrate renderer and extractor into application | Accepted | Retain; improve traceability | The integration into Infrastructure and retirement of unused imports are current. It refines the workspace-admission boundary, not a blanket prohibition on all future imports. Ensure DOC/MSG capability links identify this owner and remove active-doc claims that the old workspaces must still be built independently. |
| ADR-0026 — Enable Automation MCP by deployment configuration | Accepted | Retain activation decision; update relationships | The explicit production gate is current and matches dated ingress evidence. Its references to the older 0021 contract should lead readers to 0031 for current rights. It does not decide the new persistent-certificate/grant model by itself. |
| ADR-0027 — Authorization code for external MCP connectors | Accepted | Technical successor needed | Keep the authorization-code/PKCE and consent rationale. Ephemeral-key restart invalidation and client-only attribution conflict with the new persistent signing/encryption certificate and per-grant model in FRD-10. Record the changed lifetime, rotation and attribution boundary. |
| ADR-0028 — Integrated renderer in existing Web container | Accepted | Retain | Browser/fonts/native runtime packaging in the existing Web boundary remains coherent. It refines 0015/0025; it is not a new rendering service. Actual packaged browser execution and operator acceptance remain separate evidence. |
| ADR-0029 — Image-initiated Case projection | Accepted | Preserve projection; record new custody choice | The separate image-origin reference remains valid. Its explicit exclusion of a dedicated image-reference Box folder conflicts with FRD-05. Also correct its overbroad supersession of 0013. New custody/merge behavior requires a clear technical record while preserving both original identities and evidence. |
| ADR-0030 — Non-additive schema before cutover | Accepted | Repair expiry/activation owner | The exception is bounded by pre-cutover disposable data, not standing permission to rewrite production history. Its expiry reference leads to the old QDOS/EVA release sequence in open-decisions. Name the current cutover/consumer criterion durably, with compatible rollback/recovery evidence. |
| ADR-0031 — Automation without EVA export tools | Accepted | Retain current authority | Direct, attributed unconfirmed working-data writes are permitted; Engineer confirmation and outward dispatch are not. Do not preserve boundaries.md's blanket direct-mutation ban. Its AiWork push channel can coexist with the later AiJobs pull ledger. |
| ADR-0032 — Near-real-time durable intake triggering | Superseded | Keep history | ADR-0033 is the current scheduling successor. Fix current-decision views that stop at this intermediate record; keep historical links where they explain the actual sequence. |
| ADR-0033 — Warm unified queue for five-second intake | Accepted | Retain; separate source from live evidence | The shared queue, warm Worker and recovery design are documented technical choices. Do not infer deployed function settings or measured p95 from the record alone. Keep the distinction between normal notification work and recovery timers. |
| ADR-0034 — Per-Principal EVA API submission settings | Superseded | Keep history | ADR-0038 replaces the automatic-submission design. Old reconciliation/autosend controls must not be reintroduced from this historical record. |
| ADR-0035 — AI job ledger | Accepted | Retain both workflow boundaries | AiJobs is a pull-based job ledger, deliberately distinct from AiWork's push handoff. There is no Worker AI polling owner and the ledger itself does not mutate Case data. A broad “duplicate AI engine” finding would be unsupported. Define job-family-specific result acceptance in FRD-11. |
| ADR-0036 — Outbound mail through approved mailbox | Accepted | Refine persistence contract | Preserve staff initiation, approved-mailbox scope and observed Sent evidence. Reconcile the “no new store/second outbound record” statement with FRD-08's durable operation journal. The journal can own attempts/recovery without becoming a second authority for sent mail. |
| ADR-0037 — Linux authorized release workstation | Superseded | Keep history | ADR-0039 now supports Windows and Linux. Do not enforce the superseded Linux-only workstation rule. |
| ADR-0038 — Manual-only EVA API submission | Accepted | Retain; update old consumers | This addresses the former automatic-submission design and supports the explicit staff Review send/With Engineer re-send. Update open-decisions, runbook, capabilities and old tails accordingly. Manual re-send is not automatic retry of an uncertain provider outcome. |
| ADR-0039 — Windows and Linux release workstations | Accepted | Retain | The same exact SHA produces Linux Web/Worker artifacts and a workstation-matching migration bundle; schema-3 platform identity is coherent. Update old 0037/Windows-only procedure pointers without inventing Windows containers or an extra deployment platform. |
| ADR-0040 — Qualified Document Intelligence OCR | Accepted | Retain; propagate qualified scope | Supersedes the earlier OCR selection. PdfPig remains first; qualified scan-like and unusable-text-map PDF pages use the bounded Worker adapter and durable source identity. Include the estimate-import behavior owner in traceability. Infrastructure declarations and a selected service are not provisioning/live acceptance evidence. |

**ADR-0017 is not an issued record in the reviewed tree. Do not fill the number merely to make the sequence look complete.**

### Relationship repair rules

An active-decision view should not rely on `status: accepted` alone when the body says individual clauses are replaced. At minimum, each materially replaced clause needs an explicit successor pointer, reciprocal relationship data where supported, and a current owner whose meaning is unambiguous. A schema for partial relationships is a proposal, not permission to change existing metadata consumers without testing them.

ADRs record why a technical choice was made. They need not be continually rewritten with the latest package version, retail price, date, deployment count or operation command. Keep observed operational values and executable procedures in their own owners.

## 2. Exact ADR file index

All paths below are under `docs/adr/` at the pinned revision. This index makes each row above reproducible without relying on a mutable branch URL.

```text
0001-hybrid-pdf-extraction.md
0002-dotnet-modular-monolith-on-azure.md
0003-pdfpig-for-first-qdos-slice.md
0004-provider-api-and-staff-mcp-authentication.md
0005-multiformat-intake-assets.md
0006-provider-neutral-intake-with-contained-qdos-policy.md
0007-direct-terminal-azure-deployment.md
0008-separate-direct-provider-and-intermediary-email-policies.md
0009-adopt-pegasus-monorepo-workspaces.md
0010-adopt-single-context-domain-documentation.md
0011-restrict-mcp-to-automation-actor.md
0012-conservative-mot-mileage-estimation.md
0013-qdos-alpha-implementation-contract.md
0014-local-to-production-deployment.md
0015-host-web-on-container-apps-consumption.md
0016-standalone-desktop-email-evaluator.md
0018-provider-inspection-mode-database-setting.md
0019-in-process-onnx-vrm-recognition.md
0020-accepted-qdos-case-association-predicates.md
0021-automation-actor-direct-write-assessment-contract.md
0022-approved-mailbox-identity-and-enablement-database-setting.md
0023-restructure-repository-documentation-and-reference-evidence.md
0024-stable-approved-mailbox-identity-and-explicit-baseline.md
0025-integrate-renderer-and-extractor-into-the-application.md
0026-enable-automation-mcp-by-explicit-deployment-configuration.md
0027-authorization-code-for-external-mcp-connectors.md
0028-run-integrated-renderer-in-web-container-app.md
0029-image-initiated-case-projection.md
0030-non-additive-schema-changes-before-cutover.md
0031-automation-actor-contract-without-eva-export-tools.md
0032-near-real-time-durable-intake-triggering.md
0033-warm-unified-work-queue-for-five-second-intake.md
0034-per-principal-eva-api-submission-settings.md
0035-ai-job-ledger.md
0036-outbound-mail-via-approved-mailbox.md
0037-linux-authorised-release-workstation.md
0038-manual-only-eva-api-submission.md
0039-windows-and-linux-release-workstations.md
0040-qualified-document-intelligence-ocr.md
```

## 3. FRD-by-FRD register

### FRD-01 — Case identity and lifecycle

**Path:** `docs/frd/frd-01-case-identity-and-lifecycle.md`

**Keep:** normal Case/PO allocation distinct from the later Audit reference; immutable references and wrong-Principal replacement; native handoff independent of EVA; current D44 readiness rules; separate sign-off Engineer; attributed notes; targeted Administrator lease clearance; global Triage identity linked to, not substituted for, a formal Case.

**Correct/consolidate:** remove remaining blanket/legacy EVA-dependent explanations and copy current readiness rules into their actual consumers rather than creating another owner. Keep already-calculated chase dates distinct from the policy for future calculations: capability text implying automatic recalculation of all open work conflicts with this contract. Clarify how Inspection + Audit relates to the normal native report path rather than preserving the old EVA-only explanation.

**Coverage action:** explicit cross-references for inspection method/location and historical directory snapshots, and a clear list of changes that invalidate completeness versus report generation. Do not reintroduce the original Audit allocation gate to match FRD-02.

### FRD-02 — Intake and source identity

**Path:** `docs/frd/frd-02-intake-and-source-identity.md`

**Keep:** durable source/dispatch identities, shared allocator, grouped images, Unidentified U-references, idempotent processing, distinct transient and terminal outcomes, accepted principal/profile agreement and source-labelled correction.

**P1 corrections:** Mandatory pre-case gates still treats an Audit's original report/outcome as identity-critical to normal allocation. This conflicts with FRD-01/PRD/CONTEXT and the current QDOS companion. Request-scoped links still say 10 MiB and pending INTK-052 research despite the settled 100 MiB/20-file/200 MiB source policy. Move those settled limits from open-decisions into this owner.

**Further clauses:** “token never in message content” conflicts with sending the functional upload link in a chaser unless the security restriction distinguishes necessary recipient delivery from logs, history and unintended disclosure. Token generation/at-rest representation are called open implementation choices while other current documents record a settled hashed 256-bit token; reconcile the actual chosen security contract. Review global pre-Engineer check requirements for a cyclic market-valuation dependency. Do not merge Provider API envelope ceilings with the staff/public upload ceiling.

### FRD-03 — Triage

**Path:** `docs/frd/frd-03-triage.md`

**Keep:** separate pre-Case workflow, immutable global T-reference, independently optional findings with at least one required, reasoned superseding findings, exact reply-chain evidence where supported, cancellation and reasoned reopening. The old Needs sorting clause is repaired.

**Clarify:** wording that loosely calls Triage a Case should identify the distinct record rather than a formal Case/PO. More importantly, manual/API Triage origins must have an explicit completion-evidence contract. The email completion rule cannot simply be applied to an origin with no reply chain. Either restrict the supported origin/completion combination or define the accepted evidence; do not invent a manual “sent” proxy.

### FRD-04 — Parties, accounts and access

**Path:** `docs/frd/frd-04-parties-accounts-and-access.md`

**Keep:** roles as data rather than named-user hardcoding; access deletion/disable, forced logout/reset and targeted lease clearance; history-preserving actor and signatory records; per-Engineer Glass's credentials; Principal defaults and reusable/case party distinctions.

**P1 correction:** generated, one-time-visible temporary password here conflicts with Administrator-entered password in FRD-12/capability ACC-15. Decide and express one workflow including recovery, forced change and secret handling. Periodic account reviews have been removed; old role/UI actions must follow that change.

**Coverage/action:** preserve the new optional-qualifications rule for printed signatories instead of enforcing an old mandatory tuple everywhere. Credential encryption, session attribution and recovery belong in an appropriate technical decision, not solely a functional paragraph or stream A signature table.

### FRD-05 — Documents, extraction and custody

**Path:** `docs/frd/frd-05-documents-extraction-and-custody.md`

**Keep:** Box as durable custody, source/version identity, retained visible occurrences, original evidence preservation, closed-case restrictions and explicit custody outcomes. Qualified OCR retains document/page/source identity.

**Correct:** stale upload ceilings; current references to superseded OCR selection; dedicated image-reference Box custody versus ADR-0029's exclusion. The image-origin folder, merge and any permitted empty-folder cleanup must have an accepted exact contract, not be inferred from a broad no-delete statement or a generic provider operation.

**Coverage:** consolidate non-Case holding, logical-content reads, 24-hour idle cache, cache integrity/miss handling, failed/pending retention and re-evaluation after staging deletion. These rules are partly spread through architecture/operator notes rather than fully owned here. Do not treat a logical-content response as current acceptance when its source association or hash is inconsistent.

### FRD-06 — Vehicle and engineering evidence

**Path:** `docs/frd/frd-06-vehicle-and-engineering-evidence.md`

**Keep:** source-labelled supplied/observed/derived values; MOT mileage method; inspection location without implied attendance; image advisories distinct from lifecycle validity; independent professional findings and correction; accepted damage, valuation, estimate and settlement rules.

**Correct:** current 23-zone severity/note damage model versus old Type/region descriptions in the capability registry; defined valuation adjustments versus later deferred-adjustment wording; selected DVLA/DVSA adapters versus blanket pre-selection language. Preserve the accepted VAT/prior-total-loss/additions/condition order and rounding rules; do not derive engineering policy from a UI mockup or stale capability prose.

**Ownership:** estimate/import behavior is currently spread across this FRD, FRD-07 and FRD-11. Keep one Core calculation/import contract and route other workflows to it. Raw imported observations are not automatically accepted CE findings. Optional separation into cohesive estimate and valuation FRDs is justified only if it reduces this overlap.

### FRD-07 — EVA and external engineering handoff

**Path:** `docs/frd/frd-07-eva-and-external-engineering-handoff.md`

**Keep:** shared mapping/image selection, exact manual export schema, truthful export versus receipt evidence, explicit API outcomes, shared native Case state and staff-initiated handoff. The earlier state-unchanged wording has been corrected toward Review-to-With Engineer behavior.

**Correct:** any-save-invalidates-readiness blanket wording; old tail statements that the API awaits a usable contract; historical automatic/at-most-once wording in consumers. Native Hand to Engineer is not merely an EVA operation, so opening wording must not imply that the two EVA routes are the only engineering entry paths. Explicit re-send is intentional new work and must remain distinct from safe handling of an unknown attempt.

**Scope:** move general Glass's/Audatex raw estimate-import rules to the estimating owner; EVA is an optional adapter, not the owner of all native engineering mechanics. No first live vendor acceptance is inferred by this audit.

### FRD-08 — Email, mailbox and background processing

**Path:** `docs/frd/frd-08-email-mailbox-and-background-processing.md`

**Keep:** the confirmed taxonomy and reply rules, classification/destination separation, source evidence, mailbox generations and fresh-start boundaries, 15-minute freshness/no backfill, staff initiation and exact Submitted-versus-Sent evidence.

**P1 decisions:** reconcile mailbox/RFC Message-ID deduplication with ADR-0024's Graph immutable identity; reconcile durable send-operation journal/recovery with ADR-0036. Distinguish retained arrival facts from later provider coordinates and authoritative Sent evidence.

**Clarify:** system-wide VRM/thread association descriptions versus the principal-route matcher contract need a precise policy-selection/ownership explanation. This is a documentation conflict to inspect against the real selected policy, not proof that two business implementations exist. Keep old unavailable transport composition distinct from newly accepted staff sending.

### FRD-09 — Provider and intermediary routes

**Path:** `docs/frd/frd-09-provider-and-intermediary-routes.md`

**Keep:** the current principal-wide policy owners and evidence agreement, separate sender/issuer/intermediary/Principal roles, conservative ambiguity outcomes, principal-scoped API authorization and the defined API-01 contract. The former undefined-wire-contract and old-QDOS-version findings are repaired.

**Coverage:** make accepted Triage status/result representation explicit instead of only describing a Case-style result. Confirm the source and version of every activated profile's accepted predicates without treating a reference corpus or sender domain alone as route activation.

**Do not change:** the API's own decoded/encoded envelopes and per-attachment limits merely because manual/public uploads now allow larger files. They are different admission boundaries. An undefined historical provider_domain_key is not automatically a migration requirement.

### FRD-10 — MCP automation and actor boundary

**Path:** `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`

**Keep:** actor identity, exact scopes/rights, shared Core use cases and guards, direct unconfirmed working-data writes, no finding confirmation or outward sending, metadata authorization before content and proper versioned streaming. AI jobs and estimate import are explicit tool families.

**Correct/record:** persistent keys and per-grant/human approval attribution need the technical successor to ADR-0027. Update architecture/operations scope inventories so automation.jobs is not omitted from a current description; preserve historical inventories with dates. Do not let a discovered tool or a registered adapter count as combined caller validation.

**Ownership:** keep AiWork channel handoff distinct from AiJobs pull workflow, as ADR-0035 intends. They need not be consolidated to satisfy a superficial duplication rule.

### FRD-11 — Reports, correspondence and reviewed proposals

**Path:** `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`

**Keep:** Core-owned outcomes/calculations, immutable source-labelled generation snapshot, signatory/estimate/document/image identities, stale-generation detection, separate report/fee-note artifacts, native generation independent of EVA and staff-only dispatch.

**P1 correction:** the Report-draft entry point says Generate report draft and saves nothing, while the same FRD specifies retained generations, custody and a ready history event. Define preview as non-persisting, Generate report as the retained generation action, and approval/sending as distinct operations according to current accepted requirements. Do not describe final production generation as a draft because an old renderer slice was once draft-only.

**Coverage:** current text excludes Audit from initial activation and calls Audit parity future behavior. Reconcile with the intended v1 native Audit report scope and its actual acceptance evidence. Define MarketResearch result acceptance/terminal behavior separately from estimate-draft acceptance. Unknown repairer VAT is already an explicit requirement and must not be reported as wholly unrecorded. Signatory qualifications are optional; name/signature remain the required content specified here.

### FRD-12 — Operator experience

**Path:** `docs/frd/frd-12-operator-experience.md`

**Keep:** the integrated shell, single-scroll Case record, named sections, stage-bound actions, service health in Administration, scoped real data and no invented metrics.

**Correct:** holder-expiry display versus renewable edit authority; image-origin rows in Not ready versus Awaiting instruction; internal intake labels in display examples; old account-review controls; Administrator-entered reset password versus FRD-04; obsolete manual-upload ceilings; and the universal disabled-control ban versus required real disabled actions.

An inert integration preview is different from an implemented control disabled because the current record is ineligible. State both cases explicitly. Do not turn a project-specific accepted interaction exception into an invented universal accessibility prohibition or requirement; in particular, preserve the recorded D16 scope until deliberately changed by its authority.

Remove ticket delivery status and retain the behavior itself. Design owns visual/component rules; this FRD owns route/page interactions. Neither should silently overrule the other by an unexplained general hierarchy.

## 4. PRD register

**Files:** `docs/prd/README.md`; `docs/prd/pegasus-product.md`.

**Current strengths:** native v1 engineering/final reports, optional EVA, staff-initiated sends, Glass's repair-estimate scope, normal Case/PO followed by the later Audit reference, permanent identity/history protection and distinct evidence/acceptance states are explicitly stated.

**Repairs:** consolidate current multi-principal v1 scope rather than relying on a later operator-notes override; remove the requirement for active independently buildable imported workspaces; own permanent boundaries rather than defining them circularly through capabilities; preserve performance/cost/recovery targets as targets with scope and deferred evidence clearly identified; transfer unique support/operating-hours/commercial statements from operator-notes without inventing an SLA, absolute budget or regional-residency requirement.

No additional omnibus PRD is needed. The missing work is coherent current scope, provenance transfer and cross-document consistency, not a larger count of requirement documents.

## 5. Decisions versus missing evidence

A selected adapter without a completed live test needs an evidence task, not another selection ADR. A contradictory identity or persistence contract needs a technical decision, not merely a green unit test. A settled UI choice needs its FRD/design owner updated, not an open-decision entry saying it is not yet delivered. A valid historical record needs a date and proper authority boundary, not deletion solely because it differs from today's implementation.
