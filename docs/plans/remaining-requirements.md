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

`docs/plans/open-decisions.md` contains the decisions that remain genuinely unresolved. The predecessor and the local corpus provide evidence, not requirements.

## What is already proved locally

The first thin slice has a real Web caller at `/Intake/Qdos`. In Development, with the explicit feature flag enabled, it can:

- accept a manually selected `.eml` or `.pdf` up to 10 MB;
- read email bodies and PDF embedded text through MimeKit and PdfPig;
- let strong QDOS instruction content outrank the sender of a staff-forwarded email;
- record evidence, missing fields, conflicts, and review candidates for the ten initial instruction fields;
- default a missing instruction date from the injected clock;
- produce explicit `Confirmed QDOS`, `Needs sorting`, `OCR required`, and technical-failure outcomes;
- require an explicit case-creation authorisation before allocating a reference;
- allocate one idempotent `QDOS{YY}{NNN}` reference from the shared principal/year sequence;
- persist receipts, cases, counters, evidence, and field candidates through EF Core;
- render persisted dashboard counts, queues, and a review page;
- reject duplicate source bytes without consuming another sequence number; and
- deny every `/Intake` route outside Development or when the feature flag is not enabled.

This is local evidence only. It is not the completed intake workflow, a production-ready case record, an Azure SQL concurrency proof, a deployed application, or business acceptance of extraction accuracy.

## Required for the first QDOS release

### 1. Staff identity, permissions, and audit

- Add self-managed CollisionSpike usernames and passwords using secure non-reversible password hashes.
- Support Administrator, Engineer, and User roles. External/customer accounts are not part of the first MVP.
- Protect every page and operation except deliberately public technical health endpoints.
- Record the authenticated actor, timestamp, action, and reason for every user and automated change.
- Implement account creation, disabling, and role administration for authorised staff.
- Resolve the exact transition permission matrix before encoding irreversible workflow rules.

The development-only manual intake route must remain unavailable in a deployed environment until authentication and approved source-file custody exist.

### 2. Reviewable extraction and source custody

- Retain each original inbound email, instruction document, and attachment in the case's Box record; a hash and extracted metadata are not sufficient custody.
- Convert the current string suggestions into typed, editable, operator-confirmed case data with provenance.
- Validate at least vehicle registration, dates, mileage, claim references, and principal-specific required fields without silently truncating or guessing business values.
- Create a frozen, human-reviewed expected-value cohort and a separate untouched holdout from genuine local QDOS material.
- Report field-level accuracy, missing/conflicting values, unreadable pages, and false case-creation outcomes. A green parser test is not operator acceptance.
- Keep the original source authoritative and every extracted field reviewable.

### 3. Complete intake formats and paths

- Ingest the `instructions@collisionengineers.co.uk` shared Outlook mailbox automatically through the Worker and Microsoft Graph.
- Give every receipt a stable mailbox identity and make retries idempotent. Terminal failures must stop and become visible; transient failures may retry with a bound.
- Process PDF, DOC/DOCX, freehand email instructions, and image-led intake.
- Use embedded PDF text first and targeted Document Intelligence OCR only for pages with insufficient text.
- OCR a readable vehicle registration from image-led intake and use the registration as the provisional identifier until the principal is known.
- Classify mailbox items into `Receiving work`, `Queries`, `Other`, `Needs sorting`, or the real business `Triage` flow. `Triage` must never be used as a generic inbox label.
- Support manual case/instruction/image upload through the same Core use cases rather than a parallel rule engine.
- Deliver the required provider-facing API functionality, including the later API instruction-ingestion route, behind a versioned contract and the accepted machine-authorisation boundary.
- Deliver the required MCP functionality through the same application use cases and permission checks. Provider API and MCP clients use separately issued principal-scoped client IDs and opaque secrets, store only each secret's hash, reveal the clear value once, and support rotation/revocation. Contract boundaries and operation-level permissions still require an explicit decision.
- Send uncertain associations to `Needs sorting`; never guess a case match.

### 4. Case model and lifecycle

- Create the full QDOS case record from operator-confirmed intake data, including provider, claimant, claim, vehicle, accident, instruction date, inspection address, source, and associations.
- Create the case automatically when a definitive authorised instruction is accepted; keep uncertain material out of case creation until a staff decision resolves it.
- Support Inspection, Audit, and Inspection + Audit. Diminution and Commercial remain deferred.
- Keep one shared QDOS/year sequence across all case types.
- Produce `a.` for repairable audits and `ap.` for total-loss audits. Inspection + Audit starts with the normal inspection reference and creates the later audit reference inside the original case folder after the Engineer's finding.
- Implement incomplete/chasing, ready/review, inspection/report preparation, post-report query/dispute, and terminal behavior.
- Record roadworthiness and repairable/total-loss findings needed by the active workflow. Repair-estimate and valuation workflows remain deferred.
- Implement the three initial terminal outcomes: post-report completion, provider cancellation, and Collision Engineers rejection.
- Support reassignment, cancellation, closure, reopening with a reason, and archive. Never permanently delete a case.
- Support image/instruction matching, manual linking, and audited reversal of a mistaken merge.
- Add the configurable backend completeness gate before Engineer assignment without requiring a deployment to switch it on or off.
- Add the review gates before Engineer assignment and before a report is sent to the provider.
- Allow the inspection address to be a real vehicle/repairer address or the exact valid value `Image Based Assessment`.

### 5. Work management and operator UI

- Extract the inspection date or equivalent instruction deadline as `Due by` and show overdue work.
- Create recurring seven-day chase reminders while required information is missing; stop them when the material arrives or the case terminates.
- Generate clickable, copyable chaser text and a Box file-request link. Automated outbound sending is deferred.
- Complete the intake dashboard tiles: `Not ready`, `Review`, `Held`, `Receiving work`, `Queries`, `Other`, `Needs sorting`, `In today`, `Submitted today`, and `Cleared this week`.
- Make each count open its actual filtered queue, with last-updated time and manual refresh.
- Search and filter by Case/PO, registration, claimant, claim number, principal, stage/status, assigned Engineer, received/instruction dates, date range, and image- versus instruction-led origin.
- Preserve the original intake origin after linking or merging records.

### 6. Box, vehicle data, EVA, and email

- Create and maintain the QDOS Box case folder using the Case/PO name.
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
3. Add durable original-source custody and the Box case-folder path.
4. Add targeted OCR and the remaining PDF/DOCX/image-led formats through the same intake use case.
5. Wire the `instructions@` Graph Worker with bounded idempotent delivery.
6. Complete QDOS lifecycle, matching, due-by/chasing, dashboard, search, and EVA export.
7. Prove Azure SQL concurrency, observability, restore, and release automation in shared development.
8. Run operator acceptance on the full QDOS workflow before any production cutover.

Each increment needs a real caller, genuine-input evidence, an independent evaluator, and an operator-visible result. Code present, deployed, and accepted are separate states.
