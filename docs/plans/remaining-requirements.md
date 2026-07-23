# Remaining requirements

Status: **Pre-release planning baseline**

Last reconciled: 2026-07-23, Europe/London

Implementation baseline: `9159f8b` (`feat: add local QDOS intake vertical slice`)

## Purpose

This document says what remains between the current local proof and the first usable QDOS release. It is not a second product specification and it is not a ticket ledger.

Requirements come from, in order:

1. `docs/operator-notes/` (read-only operator truth);
2. settled answers in `PROJECT_DISCOVERY_QUESTIONNAIRE.md`;
3. accepted ADRs under `docs/architecture/decisions/`;
4. executable behavior that has been independently checked.

`docs/plans/open-decisions.md` is the canonical register for material ambiguity. Its current entries block only their named slices, not independent plan-ready work. The predecessor and the local corpus provide evidence, not requirements.

The implementation-ready domain breakdown is maintained in [remainder-delivery/](remainder-delivery/README.md). It cites this baseline rather than replacing or duplicating it.

## What is already proved locally

The first thin slice has a real Web caller at `/Intake/Qdos`. In Development, with the explicit feature flag enabled, it can:

- accept a manually selected `.eml`, `.pdf`, `.docx`, `.doc`, `.msg`, `.jpg`, `.jpeg`, or `.png` up to 10 MB;
- read email bodies, bounded nested EML, every page of each PDF plus its discrete images through MimeKit/PdfPig, and DOCX text/internal images through Open XML SDK; PDF processing is all-pages-or-incomplete under one aggregate per-intake expansion budget, never silently page-truncated;
- retain each local source plus attachment, inline image, DOCX image, and discrete PDF image as a separate review occurrence in ignored content-addressed local storage, with SQL storing metadata only;
- route legacy DOC and MSG sources to `Needs sorting` with an explicit deferred-format reason and no case/reference;
- mark only low-text, dominant-raster PDF pages as OCR candidates; ordinary image evidence is not OCR input;
- fail closed to `Needs sorting` without a reference when bounded EML processing is incomplete, even if earlier content looks confirming;
- reject DOCX packages that exceed the accepted entry, expansion, XML-part, or extracted-image limits;
- fail closed to `Needs sorting` when aggregate PDF text/image expansion exceeds the accepted limits, even if an earlier page or attachment looks confirming;
- verify local content-addressed bytes before reuse or review and refuse to serve a hash mismatch;
- let strong QDOS instruction content outrank the sender of a staff-forwarded email;
- record evidence, missing fields, conflicts, and review candidates for the ten initial instruction fields;
- default a missing instruction date from the injected clock;
- produce explicit `QDOS draft`, `Needs sorting`, `OCR required`, and technical-failure outcomes; `QDOS draft` means extraction succeeded, not that mailbox category or definitive acceptance has been decided;
- identify each manual upload by a stable channel occurrence token while retaining the SHA-256 value as integrity and possible-duplicate evidence;
- persist receipts, a relational read-only QDOS draft, assets, evidence, and field candidates through EF Core without creating a case or reference;
- render persisted dashboard counts, queues, and a review page;
- return the existing receipt for replay of the same occurrence while retaining equal bytes under different occurrence identities as separate review evidence; and
- deny every `/Intake` route outside Development or when the feature flag is not enabled.

This is local evidence only. Synthetic Web-caller checks prove the format routes, but genuine-corpus coverage remains incomplete. Local ignored artifact retention is not Box custody, a production Blob implementation, a deployed application, or business acceptance of extraction accuracy.

## Required for the first QDOS release

### 1. Staff identity, permissions, and audit

- Add self-managed CollisionSpike usernames and passwords using secure non-reversible password hashes.
- Support Administrator, Engineer, and User roles. External/customer accounts are not part of the first MVP.
- Protect every page and operation except deliberately public technical health endpoints.
- Record the authenticated actor, timestamp, action, and reason for every user and automated change.
- Implement account creation, disabling, and role administration for authorised staff.
- Allow Administrator, Engineer, and User roles to perform all case transitions and review gates; reserve account, principal, and configuration administration for Administrators.

The development-only manual intake route must remain unavailable in a deployed environment until authentication and approved source-file custody exist.

### 2. Reviewable extraction and source custody

- Retain each original inbound email, instruction document, and attachment in the case's Box record; a hash and extracted metadata are not sufficient custody.
- Convert the current string suggestions into typed, editable, operator-confirmed case data with provenance.
- Validate at least vehicle registration, dates, mileage, and claim references without silently truncating or guessing business values; show missing and contradictory values without turning them into a hard-coded completeness matrix.
- Create a frozen, human-reviewed expected-value cohort and a separate untouched holdout from genuine local QDOS material.
- Report field-level accuracy, missing/conflicting values, unreadable pages, and false case-creation outcomes. A green parser test is not operator acceptance.
- Keep the original source authoritative and every extracted field reviewable.

### 3. Complete intake formats and paths

- Ingest the `instructions@collisionengineers.co.uk` shared Outlook mailbox automatically through the Worker and Microsoft Graph.
- Give every receipt a stable mailbox identity and make retries idempotent. Terminal failures must stop and become visible; transient failures may retry with a bound.
- Process PDF, DOCX, bounded nested EML/freehand email instructions, and image-led intake. Retain DOC and MSG with provenance in `Needs sorting`; their automated extraction is deferred beyond the first MVP.
- Use embedded PDF text first and targeted Document Intelligence OCR only for scan-like pages with insufficient text and a dominant raster image.
- Keep ordinary email, DOCX, PDF-embedded, and direct image evidence reviewable without OCR. By direct product decision on 2026-07-23, automated vehicle-registration OCR/VLM is deferred beyond the first MVP; this is separate from required scanned-PDF OCR. Staff may record a readable registration as the provisional identifier until the principal is known.
- Classify mailbox items into `Receiving work`, `Queries`, `Other`, `Needs sorting`, or the real business `Triage` flow. The long-term categorisation policy is a major architectural scope: approved rules must be extensible and modifiable through one Core owner without transport-specific copies. Exact predicates and rule governance remain withheld by the open decision; do not introduce a generic engine, rule table, editor, or parallel classifier in advance. Also provide a manual `Blocked intake` filter for staff-decided blockers: retain the source and required reason/warning but create no case/reference until staff resolve and retry it. `Triage` must never be used as a generic inbox label.
- Support manual case/instruction/image upload through the same Core use cases rather than a parallel rule engine.
- Deliver a versioned provider API that uses separately issued principal-scoped client IDs and opaque secrets, stores only each secret's hash, and supports rotation/revocation. Its first-MVP operations are idempotent instruction/attachment submission plus own-submission receipt, processing status, and resulting Case/PO retrieval.
- Deliver a separate remote MCP surface for internal staff, primarily through Claude Desktop. Use per-staff OAuth, current application roles, and permanent user attribution. Expose the signed-in role's case, inbox, and document actions through the same Core use cases as the UI; exclude account/role administration, principal configuration, credential management, cloud operations, and permanent deletion.
- Send uncertain associations to `Needs sorting`; never guess a case match.

### 4. Case model and lifecycle

- Create the full QDOS case record from durably accepted definitive intake evidence, retaining the source and every available provider, claimant, claim, vehicle, accident, instruction-date, inspection-address and association value. Operator confirmation is required only to resolve material that was not already definitive or was manually blocked; it is not a universal creation gate.
- Create the case automatically when a definitive authorised instruction is accepted; keep uncertain material out of case creation until a staff decision resolves it.
- Support Inspection, Audit, and Inspection + Audit. Diminution and Commercial remain deferred.
- Keep one shared QDOS/year sequence across all case types.
- For a standalone Audit, produce `a.` or `ap.` from the repairable/total-loss assessment in the original Engineer's report. If that evidence is missing or ambiguous, retain the item in the inbox with a blocking warning and create no case/reference. Inspection + Audit starts with the normal inspection reference and creates the later Audit reference inside the original case folder after Collision Engineers' assigned Engineer records the finding.
- Implement incomplete/chasing, ready/review, inspection/report preparation, post-report query/dispute, and terminal behavior.
- Record roadworthiness and repairable/total-loss findings needed by the active workflow. Repair-estimate and valuation workflows remain deferred.
- Implement the three initial terminal outcomes: post-report completion, provider cancellation, and Collision Engineers rejection.
- Support principal reassignment on the same case before Collision Engineers sends its first report. Allocate the corrected principal's next reference for the correction year, retain the prior reference as a searchable alias, and never reuse either number. For each external artefact that already uses the old reference, require a separate audited confirmation of its manual update: Box only when the old-named folder exists, and EVA only when the old reference is present there. Block work until every applicable confirmation is complete, never for an absent artefact. After Collision Engineers sends any report for the case, keep the original identity and record the discovered error as an audit note only.
- Support image/instruction matching, manual linking, and audited reversal of a mistaken merge.
- Add the configurable backend completeness gate before Engineer assignment without requiring a deployment to switch it on or off. When enabled, it requires staff-confirmed `Instruction complete` and `Images complete`; it does not evaluate a hard-coded principal field matrix.
- Add the review gates before Engineer assignment and before a report is sent to the provider.
- Allow the inspection address to be a real vehicle/repairer address or the exact valid value `Image Based Assessment`.

### 5. Work management and operator UI

- Prevent two staff users from editing the same case at the same time. Entering edit mode must acquire one server-owned, expiring case-edit lease; another staff member may still view the case but cannot enter or save edit mode until the lease is released or expires. Every save must also present the lease token and current case version so an expired or stale editor cannot overwrite newer data. Show the lock holder and recovery state to staff, and audit material acquire/release/denial outcomes without turning heartbeats into business history.
- Extract the inspection date or equivalent instruction deadline as `Due by` and show overdue work.
- Create recurring seven-day chase reminders while required information is missing; stop them when the material arrives or the case terminates.
- Generate clickable, copyable chaser text and a Box file-request link. Automated outbound sending is deferred.
- Complete the intake dashboard tiles and filters: `Not ready`, `Review`, `Held`, `Receiving work`, `Queries`, `Other`, `Needs sorting`, manual `Blocked intake`, `In today`, `Submitted today`, and `Cleared this week`. `Not ready` is incomplete work being chased; `Review` is complete work awaiting approval; `Held` is a reasoned manual case pause that stops progression and chasers while due dates remain visible.
- Make each count open its actual filtered queue, with last-updated time and manual refresh.
- Search and filter by Case/PO, registration, claimant, claim number, principal, stage/status, assigned Engineer, received/instruction dates, date range, and image- versus instruction-led origin.
- Preserve the original intake origin after linking or merging records.

### 6. Box, vehicle data, EVA, and email

- Create and maintain the QDOS Box case folder using the Case/PO name.
- When staff correct a principal before Collision Engineers sends its first report, reconcile only external artefacts that already use the old reference. Show the Box-folder link and require an audited manual-update confirmation only when that folder exists; require the separate EVA confirmation only when EVA contains the old reference. Block until every applicable confirmation is complete, and do not automate either correction.
- Store instruction emails/documents, images, correspondence, and reports; retain prior document versions.
- Allow staff to add relevant material received through manual WhatsApp coexistence without introducing a first-MVP WhatsApp integration.
- Make files read-only at the application level when a case closes and require an audited reopen before revision or logical removal.
- Create Box file requests for missing information or images.
- Add DVLA/DVSA vehicle and MOT lookup when details are absent, including mileage estimation when the source data supports it.
- Export operator-approved structured case JSON and the stored image bundle for manual transfer to EVA.
- Keep EVA authoritative for Engineer assignment, estimating, valuation, and report generation until an approved replacement slice exists.
- Associate related emails and attachments with the case and provide the required first-MVP in-app email management for the mailbox scope.

### 7. Azure and release readiness

- Prove the committed EF migration and reference-allocation transaction against SQL Server/Azure SQL, including concurrent allocation, duplicate delivery, rollback, and sequence exhaustion.
- Apply production migrations as an explicit release operation; application startup must not silently mutate the production schema.
- Wire Web and Worker to the same Core behavior and Infrastructure adapters. A registered but uncalled Worker service is unfinished.
- Use managed identity and scoped RBAC between Azure services. Store only third-party secrets that cannot use identity in Infisical or Key Vault.
- Add correlated Web/Worker telemetry, readiness checks for real dependencies, bounded failure handling, and alerts for the business/integration failures listed in the questionnaire.
- Prove database restore and the documented four-hour restoration path before production acceptance.
- Add scoped GitHub OIDC deployments for the separate shared-development and production resource groups.
- Run Bicep/azd preview, policy/quota checks, health probes, and a non-sensitive smoke path before any approved Azure deployment.
- Obtain explicit user approval before provisioning chargeable resources, deploying, changing Azure, or retiring predecessor resources.

CollisionSpike v2 starts fresh. No predecessor cases, users, audit records, or application state are imported. The predecessor was pre-release, so preserving or reconciling its test application data is not a v2 release requirement. Retirement of its Azure resources is a separate, exact-target operation that still requires explicit approval and protection of any shared assets.

## Explicitly deferred beyond the first MVP

- repair-estimate, valuation, invoice, accounting, and direct estimating-service workflows;
- Diminution and Commercial case processing;
- direct EVA API integration and eventual EVA replacement;
- guided claimant/mobile image capture, Tractable, and Ravin;
- in-app AI and image/vision assistance;
- automated vehicle-registration OCR/VLM and automated extraction of legacy DOC/MSG containers;
- automated outbound chasers and WhatsApp ingestion/automation;
- automated malware scanning;
- inspection-address prediction or mapping assistance;
- a custom Collision Engineers subdomain;
- external/customer accounts;
- multi-region failover, zone redundancy, private networking, staging, and deployment slots.

Deferred features may have clean seams in the current architecture, but they must not add dormant services, duplicate engines, speculative projects, or first-MVP release gates.

## Recommended delivery order

1. Turn the current QDOS receipt into a human-approved, typed case draft backed by a field-expectation cohort and holdout.
2. Add staff authentication, roles, and the permanent audit actor so subsequent case changes have real ownership.
3. Replace local ignored artifact retention with durable original-source custody and the Box case-folder path; use private Blob staging with managed identity for Worker processing.
4. Add targeted Document Intelligence OCR for the persisted scan-like PDF page candidates through the same intake use case.
5. Wire the `instructions@` Graph Worker with bounded idempotent delivery.
6. Complete QDOS lifecycle, exclusive case editing, matching, due-by/chasing, dashboard, search, and EVA export.
7. Prove Azure SQL concurrency, observability, restore, and release automation in shared development.
8. Run operator acceptance on the full QDOS workflow before any production cutover.

Each increment needs a real caller, genuine-input evidence, an independent evaluator, and an operator-visible result. Code present, deployed, and accepted are separate states.
