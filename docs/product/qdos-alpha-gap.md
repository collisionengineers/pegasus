# `0.1.0-alpha.1` gap

Status: **Active `0.1.0-alpha.1` gap baseline**

Last reconciled: 2026-07-26, Europe/London

Source/caller baseline: `d68bed7501e3a30abd1f32835c0de96ba90801e5`, inspected 2026-07-26. This identifies current source paths; it is not fresh runtime, deployment or operator evidence.

## Purpose

This document says what remains between the current local proof and the `0.1.0-alpha.1` live QDOS alpha release. It is not a second product specification, roadmap, or ticket ledger. Allocation is owned by the [capability inventory](capabilities.md); a horizon or version is not implementation or acceptance evidence.

Requirements come from, in order:

1. `docs/operator-notes/` (authoritative operator truth; documentation and organization are maintainer-editable under user authorization);
2. settled answers in `docs/history/product/project-discovery-questionnaire.md`;
3. accepted ADRs under `docs/architecture/decisions/`;
4. executable behavior that has been independently checked.

The [open-decision register](open-decisions.md) owns material ambiguity. Its current entries block only their named slices, not independent decision-complete work. The predecessor and the local corpus provide evidence, not requirements.

The former [`0.1.0-alpha.1` delivery pack](../history/plans/remainder-delivery/README.md) and [delivery roadmap](../history/plans/delivery-roadmap.md) are historical planning evidence. Current outcomes are routed through product areas and the [roadmap](../roadmap.md); selected work requires a new change record.

<a id="what-is-already-proved-locally"></a>

## What current source and prior local evidence establish

The first thin slice has a source-mapped Web caller at `/Intake/Upload`. In Development, with the explicit provider-neutral feature flag enabled, the prior local evidence and current source establish that it can:

- accept a manually selected `.eml`, `.pdf`, `.docx`, `.doc`, `.msg`, `.jpg`, `.jpeg`, or `.png` up to 10 MB;
- read email bodies, bounded nested EML, every page of each PDF plus its discrete images through MimeKit/PdfPig, and DOCX text/internal images through Open XML SDK; PDF processing is all-pages-or-incomplete under one aggregate per-intake expansion budget, never silently page-truncated;
- retain each local source plus attachment, inline image, DOCX image, and discrete PDF image as a separate review occurrence in ignored content-addressed local storage, with SQL storing metadata only;
- route legacy DOC and MSG sources to `Needs sorting` with an explicit deferred-format reason and no case/reference;
- mark only low-text, dominant-raster PDF pages as OCR candidates; ordinary image evidence is not OCR input;
- fail closed to `Needs sorting` without a reference when bounded EML processing is incomplete, even if earlier content looks confirming;
- reject DOCX packages that exceed the accepted entry, expansion, XML-part, or extracted-image limits;
- fail closed to `Needs sorting` when aggregate PDF text/image expansion exceeds the accepted limits, even if an earlier page or attachment looks confirming;
- verify local content-addressed bytes before reuse or review and refuse to serve a hash mismatch;
- invoke one contained QDOS extraction policy only after a source is fully readable, let strong QDOS instruction content outrank the sender of a staff-forwarded email, and never use QDOS as the default principal;
- record evidence, missing fields, conflicts, and review candidates for the ten initial instruction fields;
- default a missing instruction date from the injected clock;
- produce explicit `Draft ready`, `Needs sorting`, `OCR required`, `Unsupported`, and retryable technical-failure outcomes; `Draft ready` means the QDOS extraction policy produced a reviewable instruction draft, not that mailbox category or definitive acceptance has been decided;
- identify each manual upload by a stable channel occurrence token while retaining the SHA-256 value as integrity and possible-duplicate evidence;
- persist provider-neutral receipts, a relational read-only instruction draft, assets, evidence, field candidates, and the extraction policy key/version through EF Core without creating a case or reference;
- initialise a fresh provider-neutral SQLite schema at `artifacts/local/pegasus-development.db`; refuse old or mismatched local migration/schema baselines before mutation and leave the former local database path untouched;
- render persisted dashboard counts, queues, and a review page;
- return the existing receipt for replay of the same occurrence while retaining equal bytes under different occurrence identities as separate review evidence; and
- deny every `/Intake` route outside Development or when the feature flag is not enabled, and keep the retired `/Intake/Qdos` route unavailable even when local intake is enabled.

This is local evidence only. A pinned genuine-corpus regression sample traverses the Web caller, but it is not a complete human-reviewed field-accuracy cohort or untouched holdout. Local ignored artifact retention is not Box custody, a production Blob implementation, a deployed application, or business acceptance of extraction accuracy.

## Required for the first QDOS release

### 1. Staff identity, permissions, and permanent action history

- Add self-managed Pegasus usernames and passwords using secure non-reversible password hashes.
- Support Administrator, Engineer, and User roles. External/customer accounts are not part of the `0.1.0-alpha.1`.
- Protect every page and operation except deliberately public technical health endpoints.
- Record business mutations, downloads/exports, material denied or failed business actions, automated business results, and external information actually accepted, linked, or used in permanent action history. Store structured before/after values, actor, time, required reason, and outcome without secrets or file/message bodies. Keep routine views/searches/refreshes and polling/retry/lease/heartbeat/adapter mechanics in content-safe telemetry; keep sign-ins in the security log.
- Implement account creation, disabling, and role administration for authorised staff.
- Allow Administrator, Engineer, and User roles to perform all case transitions and the pre-Engineer-assignment review gate; reserve account, principal, configuration, and approved-mailbox allowlist administration for Administrators.

The development-only manual intake route must remain unavailable in a deployed environment until authentication and approved source-file custody exist.

### 2. Reviewable extraction and source custody

- Retain each original inbound email, instruction document, and attachment in the case's Box record; a hash and extracted metadata are not sufficient custody.
- Convert the current string suggestions into typed, editable, operator-confirmed case data with provenance.
- Validate at least vehicle registration, dates, mileage, and claim references without silently truncating or guessing business values; show missing and contradictory values without turning them into a hard-coded completeness matrix.
- Create a frozen, human-reviewed expected-value cohort and a separate untouched holdout from genuine local QDOS material.
- Report field-level accuracy, missing/conflicting values, unreadable pages, and false case-creation outcomes. A green parser test is not operator acceptance.
- Keep the original source authoritative and every extracted field reviewable.

### 3. Complete intake formats and paths

- Ingest staff-forwarded work from the `instructions@collisionengineers.co.uk` shared Outlook mailbox automatically through the Worker and Microsoft Graph. Preserve the forwarded source; do not classify from the transport sender alone.
- Give every receipt a stable mailbox identity and make retries idempotent. Terminal failures must stop and become visible; transient failures may retry with a bound.
- Process PDF, DOCX, bounded nested EML/freehand instructions, and image-led intake in `0.1.0-alpha.1`. Retain DOC and MSG with provenance in `Needs sorting`; their automated extraction is `Next`/`unallocated`.
- Keep embedded PDF text and images reviewable in `0.1.0-alpha.1`. Targeted OCR for scan-like PDF pages is `Next`/`unallocated`.
- Automatically read vehicle registration from ordinary vehicle images in `0.1.0-alpha.1` while retaining originals, provenance, uncertainty, and operator review. Do not infer whether the implementation is OCR, VLM, or another mechanism. Broader image/damage AI or vision assistance is `Next`/`unallocated`.
- Treat mailbox categorisation and every automatic email-matching path as the single evidence-governance research area routed through [the mailbox dossier](../history/plans/mailbox-categorisation-and-email-matching/README.md). `0.0.0-development`/`0.1.0-alpha.1` must prove one Core orchestration owner with separate code-versioned direct-provider and intermediary policies through the local EML evaluator; a provider may be reached by both routes. `0.1.0-alpha.1` reuses that owner for staff-forwarded `instructions@`; `Next`/`unallocated` expands it across all four mailboxes, detailed classifications, queues, folders, and actions. Until the applicable route's evidence is accepted, retain sources visibly without guessed categories/matches and do not add a generic engine, rule table, editor, or transport-specific parallel classifier. Manual `Blocked intake` retains the source and required reason/warning but creates no case/reference. `Triage` is never a generic inbox label.
- Support Triage as a separate `0.1.0-alpha.1` pre-case record. An active record requires a vehicle registration; otherwise retain the source in `Needs sorting`. Its states are `Open`/`Awaiting information` -> `Finding recorded` -> `Completed`, with a binary `Roadworthy`/`Unroadworthy` finding; `Cancelled` is the only end without a finding. It has an optional assignee, no due date, and no chasers.
- Require the exact reply-chain Outlook Sent item from an approved mailbox to complete Triage; do not fall back to subject, registration, or manual message selection. Before send, finding replacement requires a reason. After send, store a superseding finding, require a new response, and keep full history. Reopen always to `Open`.
- Keep each Triage separate when linked to a later case. Auto-link only after the combined research accepts a definitive shared match; otherwise staff confirm. A Triage links to at most one case, a case may link multiple Triage records, and any staff role may unlink/relink with a reason.
- Support manual case/instruction/image upload through the same Core use cases rather than a parallel rule engine.
- Preserve the principal-scoped provider API contract for `Next`/`unallocated`: separately issued client IDs and opaque secrets, secret hashes only, rotation/revocation, idempotent submission, and own receipt/status/result retrieval. It is not a `0.1.0-alpha.1` release gate.
- Deliver the `0.1.0-alpha.1` remote staff MCP, primarily through Claude Desktop, with per-staff OAuth, current roles, and permanent attribution. `0.1.0-alpha.1` tools cover case, document, and intake-queue actions through the same Core use cases as the UI; broader classified-email actions are `Next`/`unallocated`. Exclude administration, configuration, credential management, cloud operations, and permanent deletion.
- Send uncertain associations to `Needs sorting`; never guess. Automatic email and image/instruction association is `Next`/`unallocated`, except the separately allocated `0.1.0-alpha.1` exact report and Triage evidence matchers after the combined research is accepted.

### 4. Case model and lifecycle

- Create the full QDOS case record from durably accepted definitive intake evidence, retaining the source and every available provider, claimant, claim, vehicle, accident, instruction-date, inspection-address and association value. Operator confirmation is required only to resolve material that was not already definitive or was manually blocked; it is not a universal creation gate.
- Create exactly one incomplete `Not ready` case automatically when a definitive authorised instruction is accepted; keep uncertain material out of case creation until a staff decision resolves it. Only explicit staff confirmation of separate instruction and image completeness moves that existing case to `Review`; do not infer automatic completeness from the instruction itself.
- Support Inspection, Audit, and Inspection + Audit. Diminution and Commercial remain deferred.
- Keep one shared QDOS/year sequence across all case types.
- For a standalone Audit, produce `a.` or `ap.` from the repairable/total-loss assessment in the original Engineer's report. If that evidence is missing or ambiguous, retain the item in the inbox with a blocking warning and create no case/reference. Inspection + Audit starts with the normal inspection reference and creates the later Audit reference inside the original case folder after Collision Engineers' assigned Engineer records the finding.
- Implement `0.1.0-alpha.1` incomplete/chasing, ready/review, tracked inspection/report progress, and all four terminal outcomes. The post-report query/dispute workspace is `Next`/`unallocated`.
- Record roadworthiness and repairable/total-loss findings needed by the active workflow. Repair-estimate and valuation workflows remain deferred.
- Implement four initial terminal outcomes: post-report completion, provider cancellation, Collision Engineers rejection, and `Created in error` for wrong-principal allocation.
- Make a used principal code immutable. A legitimate replacement creates a new linked principal and atomically deactivates the predecessor. Continue the predecessor's next sequence number in the cutover year; start later years at `001`.
- Make the case principal/reference immutable immediately on allocation. A wrong-principal allocation closes the erroneous original as `Created in error`, requires a reason and link to a new replacement case under the corrected principal, never reuses either reference, and never permits the original to reopen.
- Support reasoned reopening from a closed case to any otherwise-valid nonterminal workflow state, with normal destination gates. Exclude `Held`, which uses its separate action, and prohibit reopening `Created in error`.
- Support `0.1.0-alpha.1` manual image/instruction linking and reasoned permanent-history reversal. Automatic matching is `Next`/`unallocated`.
- Add the configurable backend completeness gate before Engineer assignment without requiring a deployment to switch it on or off. When enabled, it requires staff-confirmed `Instruction complete` and `Images complete`; it does not evaluate a hard-coded principal field matrix.
- Add the configurable review gate before Engineer assignment. Do not add a pre-send report review gate. `0.1.0-alpha.1` detects exact report evidence but does not send; automatic report sending is `Later`/`unallocated` and requires a separate accepted contract.
- Allow the inspection address to be a real vehicle/repairer address or the exact valid value `Image Based Assessment`.

### 5. Work management and operator UI

- Prevent two staff users from editing the same case at the same time. Entering edit mode must acquire one server-owned, expiring case-edit lease; another staff member may still view the case but cannot enter or save edit mode until the lease is released or expires. Every save must also present the lease token and current case version so an expired or stale editor cannot overwrite newer data. Show the lock holder and recovery state to staff. Record only material business denials/failures in permanent action history; keep lease mechanics and heartbeats in content-safe telemetry.
- Extract the inspection date or equivalent instruction deadline as `Due by` and show overdue work.
- Schedule the first chase for the same Europe/London local clock time exactly seven calendar days after entering `Not ready`, then continue the seven-calendar-day cadence while information remains missing. Entering `Held` preserves the prior state and any remaining local-clock interval. Release offers the prior state or `Review`; returning to `Not ready` resumes the preserved remainder, while `Review` ends the chase. Material arrival or terminal closure also stops future chasers.
- Generate clickable, copyable chaser text and a Box file-request link. Automated outbound sending is deferred.
- Complete the `0.1.0-alpha.1` dashboard with `Not ready`, `Review`, `Held`, `Needs sorting`, manual `Blocked intake`, a separate Triage route, `In today`, paired `Sent to Engineer` today/week, and paired `Reports sent` today/week. Categorised `Receiving work`, `Queries`, and `Other` email queues are `Next`/`unallocated`. Use Europe/London midnight days and Monday-to-Monday weeks. `In today` counts cases created; `Sent to Engineer` counts once from first successful EVA JSON/image export generation as a proxy that does not prove EVA receipt; later EVA replacement records actual assignment. `Reports sent` counts every successfully sent report.
- Make each count open its actual filtered queue, with last-updated time and manual refresh.
- Search and filter by Case/PO, registration, claimant, claim number, principal, stage/status, assigned Engineer, received/instruction dates, date range, and image- versus instruction-led origin.
- Preserve the original intake origin after linking or merging records.

### 6. Box, vehicle data, EVA, and email

- Create and maintain the QDOS Box case folder using the Case/PO name.
- Store instruction emails/documents, images, correspondence, and reports; retain prior document versions.
- Allow staff to add relevant material received through manual WhatsApp coexistence without introducing a `0.1.0-alpha.1` WhatsApp integration.
- Make files read-only at the application level when a case closes and require a reasoned reopen recorded in permanent action history before revision or logical removal.
- Create Box file requests for missing information or images.
- Add DVLA/DVSA vehicle and MOT lookup when details are absent, including mileage estimation when the source data supports it.
- Export operator-approved structured case JSON and the stored image bundle for manual transfer to EVA.
- Keep EVA authoritative for Engineer assignment, estimating, valuation, and report generation until an approved replacement slice exists.
- In `0.1.0-alpha.1`, detect but do not send reports. Prove sending with one exact Outlook Sent item from the approved allowlist. Automatic exact-item matching is a `0.1.0-alpha.1` gate under the combined research; absent/ambiguous evidence uses the settled staff link with a required reason. Outlook `sentDateTime` is authoritative; discovery/link times remain separate. Any staff role may unlink/relink with a reason and recompute dependent events/counts; confirmed evidence remains final after an Outlook move/delete. Automatic sending is `Later`/`unallocated`.
- Automatic general email/case association is `Next`/`unallocated` after the combined research is accepted; provide manual review through shared Core use cases meanwhile.

### 7. Azure and release readiness

- Prove the committed EF migration and reference-allocation transaction against SQL Server/Azure SQL, including concurrent allocation, duplicate delivery, rollback, and sequence exhaustion.
- Apply production migrations as an explicit release operation; application startup must not silently mutate the production schema.
- Wire Web and Worker to the same Core behavior and Infrastructure adapters. A registered but uncalled Worker service is unfinished.
- Use managed identity and scoped RBAC between Azure services. Store only third-party secrets that cannot use identity in Infisical or Key Vault.
- Add correlated Web/Worker telemetry, readiness checks for real dependencies, bounded failure handling, and alerts for the business/integration failures listed in the questionnaire.
- Prove database restore and the documented four-hour restoration path before production acceptance.
- Deploy committed Bicep through `azd` from an authorised terminal. GitHub Actions/OIDC deployment is `Not planned`.
- Run Bicep/azd preview, policy/quota checks, health probes, and a non-sensitive smoke path before any approved Azure deployment.
- Obtain explicit user approval before provisioning chargeable resources, deploying, changing Azure, or retiring predecessor resources.

Pegasus `Next`/`unallocated` starts fresh. No predecessor cases, users, action-history records, or application state are imported. The predecessor was pre-release, so preserving or reconciling its test application data is not a `Next`/`unallocated` release requirement. Retirement of its Azure resources is a separate, exact-target operation that still requires explicit approval and protection of any shared assets.

## Allocated beyond `0.1.0-alpha.1`, conditional, unclear, or never

The former [later-delivery pack](../history/plans/later-delivery/README.md) is historical activation evidence. Permanent and unallocated behavior is owned by the non-implementation [boundary contract](boundaries.md); a row there is not a backlog item. Any promoted outcome starts with a current change record.

- **`Next`/`unallocated`:** additional providers through the same bounded provider-neutral workflow.
- **`Next`/`unallocated`:** provider API; four-mailbox email workspace/management; general email/image matching; DOC/MSG extraction; scan-like PDF OCR; post-report query/dispute work; image/damage AI or vision assistance.
- **`Later`/`unallocated`:** Diminution, Commercial, automated WhatsApp, automated chasers, in-app assistant, and conditional AI suggestions where rules are insufficient.
- **`Later`/`unallocated`:** conditional direct EVA API, EVA replacement, estimating/valuation/invoice/report functions, automatic reports, and staff-selected AI Assessor.
- **Unclear:** own guided capture, Tractable/Ravin, and custom domain.
- **Never:** external/customer accounts, malware scanning, SMS, Teams, portal, predecessor reuse/import/operation after cutover, separate QA/UAT/staging/demo/training environments, GitHub Actions/OIDC, slots/S1, private networking, zone/multi-region resilience, and quarterly recovery exercises.

Deferred features may have clean seams in the current architecture, but they must not add dormant services, duplicate engines, speculative projects, or `0.1.0-alpha.1` release gates.

## Recommended delivery order

Use the canonical [roadmap](../roadmap.md). In summary: establish the relational draft and trusted actors; prepare principal/reference data and durable custody; prove ordinary-image registration and provisional pre-case identity; establish the allocator before the single acceptance transaction; then add Box, editing, lifecycle, the approved UI, a real Graph Worker trigger, Triage, vehicle/address/EVA, staff MCP, Azure/recovery and actual operator/management acceptance. Later work follows only through its allocated outcome and a new change record.

Each increment needs a real caller, genuine-input evidence, an independent evaluator, and an operator-visible result. Code present, deployed, and accepted are separate states.
