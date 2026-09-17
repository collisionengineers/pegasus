# Capability inventory

Stable capability identities link to their durable requirements. The owning
PRD/FRD/ADR defines scope and behavior; this registry does not allocate a release,
record delivery status or supply an independent acceptance roster. The current
operator task and linked PR/CI records own work ordering and delivery evidence.
A listed identity does not imply current activation or acceptance.

| ID | Durable outcome | Canonical owner |
| --- | --- | --- |
| OPS-10 | Production environment deployed directly from an authorised terminal | [ADR-0014](adr/0014-local-to-production-deployment.md) |
| OPS-22 | Genuine-corpus local evaluation harness | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-01 | Local development-only EML categorisation evaluator over a read-only local working copy, recording adjudications into the local `emailevallocal` tree ([ADR-0016](adr/0016-standalone-desktop-email-evaluator.md)) | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-02 | Reviewer selects from the detailed Received/Sent/Reply taxonomy and records required reasoning | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-03 | `Other` category lets the reviewer enter a new category name and reasoning | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-04 | Copying the reviewed EML into the local `emailevallocal` tree and appending the JSONL adjudication log records the human result; source files are never moved or modified ([ADR-0016](adr/0016-standalone-desktop-email-evaluator.md)) | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-05 | Display the rule-generated category and evidence beside the human review once rules exist | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| MAIL-20 | Run live provider-specific instruction-email categorisation against `.eml` files in the local folder-based evaluator | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| MAIL-21 | Minimum shared Core classification foundation: versioned rules, decision evidence, ambiguity outcome, and acceptance cohort | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-22 | User-confirmed detailed Received/Sent categories and subtypes, mirrored Reply classifications, `Other` name/reason behavior, and category/destination separation | [Settled mailbox taxonomy and correction](frd/frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction) |
| ACC-01 | Staff sign-in with Pegasus-managed usernames and passwords | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-02 | Administrator superuser, Engineer, and User roles; Andrew and Alex are initial Administrator assignments held as data/configuration, never hard-coded authorization | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-03 | Staff account creation, disable/delete access, password reset, force logout, targeted lease clearance, and role assignment | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-04 | Role-based protection for every non-public page and action | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-05 | Contacts and Principal policy administration | [Contacts administration](frd/frd-04-parties-accounts-and-access.md#contacts-administration) |
| ACC-06 | Principal-code replacement with linked predecessor and sequence continuity | [Principal and case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| ACC-07 | Application and workflow configuration managed by Administrators | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-08 | Approved Outlook mailbox allowlist managed by Administrators | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-09 | Permanent action history for business changes, exports, and material failures | [Permanent action history](frd/frd-04-parties-accounts-and-access.md#permanent-action-history) |
| ACC-10 | Separate authentication/security log | [Permanent action history](frd/frd-04-parties-accounts-and-access.md#permanent-action-history) |
| ACC-15 | Administrator-initiated staff password reset using a generated temporary password visibly revealed to the Administrator and the existing forced-change state | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-11 | Operational telemetry (`content-safe` wording considered unnecessary) | [Permanent action history](frd/frd-04-parties-accounts-and-access.md#permanent-action-history) |
| INT-01 | Manual upload of instruction emails, documents, and vehicle images | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
| INT-02 | Automatic ingestion from `instructions@collisionengineers.co.uk` | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
| INT-03 | Correct handling of staff-forwarded email as real intake | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
| INT-08 | Stable source identity, duplicate-delivery handling, and idempotent retry | [Source occurrence and dispatch identity](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity) |
| INT-09 | Original inbound source and attachment custody | [Source occurrence and dispatch identity](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity) |
| INT-10 | EML and freehand email-body extraction | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-11 | PDF embedded-text and embedded-image extraction | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-12 | DOCX text and every visible image-placement extraction, without deduplicating repeated appearances | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-13 | JPEG and PNG image-led intake | [Grouped image-intake routing](frd/frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing) |
| INT-17 | Automatic vehicle-registration reading from ordinary vehicle images | [Ordinary-image VRM and image analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) |
| INT-18 | Bounded, fail-closed processing for unreadable, oversized, or incomplete sources | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-19 | Typed, editable, operator-reviewable extracted case draft | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-20 | Field provenance, validation, missing-value, and contradiction display | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-21 | Human-reviewed extraction cohort, holdout, and field-level accuracy reporting | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-22 | Automatic identification of the correct principal/provider | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| INT-23 | Unidentified queue with immutable U-references for uncertain or unsupported intake; a missing Audit original report is a Case requirement, not an Unidentified reason | [Unidentified destination and reference](frd/frd-02-intake-and-source-identity.md#unidentified-destination-and-reference) |
| INT-24 | Material that cannot become a Case: Unidentified Close with reason and Reopen, Could not be read items, and Retry allocation, Retry OCR and Re-evaluate on Operations and the Intake log | [Mandatory pre-case gates](frd/frd-22-pre-case-gates-matching-and-association.md#mandatory-pre-case-gates) |
| INT-25 | Automatic case creation from definitive authorised intake | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| INT-26 | Manual case creation through the same business rules | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| INT-27 | Registration-based provisional identity for image-led work | [Image-initiated Case projection](frd/frd-19-image-led-intake-and-pairing.md#image-initiated-case-projection) |
| INT-29 | Manual linking and reasoned reversal of a mistaken match/merge | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| INT-30 | Preservation of original intake origin after linking or merging | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| MAIL-14 | Detect an exact Outlook Sent item as report-sent evidence | [Outbound correspondence evidence](frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence) |
| MAIL-15 | Manually link, unlink, or relink an exact Sent item with a reason | [Outbound correspondence evidence](frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence) |
| MAIL-16 | Automatically match the exact report Sent item to its case | [Outbound correspondence evidence](frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence) |
| MAIL-18 | Generate copyable chaser messages for staff to send manually | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| TRI-01 | Distinct inbox Triage label and separate pre-case record/workflow | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-02 | Vehicle-registration gate and Triage-specific missing-registration behavior | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-03 | Open, Awaiting information, Finding recorded, Completed, and Cancelled states | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-04 | Roadworthiness and Assessment are independently optional; at least one is required per recorded finding; later correction is a reasoned superseding finding | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-05 | Outcome-based completion and optional Reply with outcome correspondence | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-06 | Reopen and superseding-finding behavior with permanent history | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-07 | Optional later case link, unlink, and relink | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-08 | Dedicated Triage list and detail workspace | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-09 | Optional Triage assignee, with no due date and no chasers | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| CASE-01 | Every active QDOS case type can travel end to end from intake through accepted case workflow to successful EVA export/handoff | [States and labels](frd/frd-13-case-lifecycle-and-workflow.md#states-and-labels) |
| CASE-02 | Inspection cases | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-03 | Standalone Audit cases allocate one `a.` Case/PO with or without the original report and show the missing report as an outstanding Case requirement | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-04 | Inspection + Audit: an Inspection Case and its linked Audit Case with its own `a.` reference, created by Create audit | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-07 | Shared principal/year case sequence | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-08 | One `a.` Audit reference prefix, independent of assessment outcome | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-09 | Case principal and reference immutability after allocation | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-10 | Wrong-principal `Created in error` closure and linked replacement case | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-11 | Typed provider, claimant, claim, vehicle, accident, contact, and inspection data | [Case types and accepted Case projection](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-12 | Relationships to staff/Engineer, repairer/bodyshop, insurer, and contacts | [Principal and historical case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-13 | Instruction completeness and image completeness are separate configured sets of required items, each shown as named blockers | [Readiness and Review](frd/frd-13-case-lifecycle-and-workflow.md#readiness-and-review) |
| CASE-14 | Mandatory instruction-completeness and image-completeness gate before Engineers-queue eligibility | [Readiness and Review](frd/frd-13-case-lifecycle-and-workflow.md#readiness-and-review) |
| CASE-15 | Not ready becomes Review automatically when every required item is present; there is no staff act of reviewing instructions or images | [Readiness and Review](frd/frd-13-case-lifecycle-and-workflow.md#readiness-and-review) |
| CASE-16 | `Not ready`, `Review`, `With Engineer`, `Completed`, `Query` and `Held` workflow | [States and labels](frd/frd-13-case-lifecycle-and-workflow.md#states-and-labels) |
| CASE-17 | Due-by date extraction and overdue display | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-18 | Configurable whole-calendar-day missing-information chase schedule | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-19 | Hold/release behavior that preserves the chase interval | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-20 | General case tasks and reminders | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-21 | Every successful manual EVA export records replay-safe action history; the first also records the once-per-case `First sent to Engineer` proxy | [Focused EVA manual handoff](frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff) |
| CASE-24 | Post-report completion, provider cancellation, and Collision Engineers rejection outcomes | [Close case](frd/frd-13-case-lifecycle-and-workflow.md#close-case) |
| CASE-25 | Reasoned return to engineering through normal destination gates | [Actions](frd/frd-13-case-lifecycle-and-workflow.md#actions) |
| CASE-26 | Archive without permanent case deletion | [Archive](frd/frd-13-case-lifecycle-and-workflow.md#archive) |
| CASE-27 | Exclusive edit lease and stale-write protection | [Case edit lease](frd/frd-14-record-edit-leases.md#case-edit-lease) |
| CASE-28 | Independent Roadworthiness and Assessment findings with reasoned correction, preserved versions, authorized revision, and no inferred fee/invoice change | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| CASE-29 | Provider-determined inspection mode: the Principal's persisted setting autofills exact `Image Based Assessment` at Case creation or requires an operator-confirmed physical address, with reasoned per-Case staff override | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| CASE-30 | Track native inspection/report work with optional EVA handoff | [Focused EVA manual handoff](frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff) |
| CASE-32 | Sign-off Engineer as a Case field beside Engineer, offered from flagged staff accounts, defaulting to the assigned Engineer when flagged and otherwise the account flagged as default Sign-off Engineer; the account setting carries the flag, qualifications and signature image; reports render the sign-off tuple | [Sign-off Engineer](frd/frd-13-case-lifecycle-and-workflow.md#sign-off-engineer); [Staff accounts](frd/frd-04-parties-accounts-and-access.md#staff-accounts) |
| CASE-34 | Inspect at as a fast-update choice (Image Based Assessment, Claimant address, Repairer location, Storage location, previous addresses used for this Principal, Manual entry) and a storage location on the Case | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| UI-01 | Operations dashboard/cockpit | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| UI-02 | Case queues for Not ready, Review, and Held | [Cases: queues and filters](frd/frd-15-work-centre-queues-and-search.md#cases-queues-and-filters) |
| UI-03 | E-mail activity for Unidentified, including closed and could-not-be-read items | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| UI-04 | New cases today, Sent to Engineer, and Reports sent day/week activity | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| UI-05 | Click-through filtered work queues | [Work Centre](frd/frd-15-work-centre-queues-and-search.md#work-centre) |
| UI-06 | Last-good time, distinct current/stale/partial/unavailable/failed states, auditable reconciliation, and manual refresh | [Dashboard freshness and reconciliation](frd/frd-15-work-centre-queues-and-search.md#dashboard-freshness-and-reconciliation) |
| UI-07 | Search/filter by reference (Case/PO or Image reference), registration, claimant, claim, principal, state, Engineer, dates, and origin | [Search](frd/frd-15-work-centre-queues-and-search.md#search) |
| UI-08 | Three-column intake review workbench | [Upload confirmation surface](frd/frd-18-manual-upload-and-upload-links.md#upload-confirmation-surface) |
| UI-09 | Full case workspace | [Case workspace](frd/frd-16-case-record-workspace.md#case-workspace) |
| UI-11 | Accounts, Contacts, mailbox allowlist, and configuration workspace | [Administration](frd/frd-17-administration-workspace.md#administration) |
| UI-13 | Accessible keyboard, screen-reader, focus, contrast, and error behavior | [Operator experience](frd/frd-12-operator-experience.md#operator-experience) |
| UI-16 | Integrated Operations Workspace shell: one persistent rail (Work Centre, Inbox, Upload, Cases, Search, Operations, Administration) with live counts and rail collapse, Work Centre needs-attention work, personal notifications, Cases queue groups, the working-set strip of open records, command palette and breakpoints | [Shell and routes](frd/frd-12-operator-experience.md#shell-and-routes) |
| UI-17 | Case record with Scroll and Tabs display modes over one edit form, sticky identity/action/section navigation in Scroll, lazily rendered sections, `?section=` jumps, and the ten sections Overview, Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report, Files, Notes; the Engineer workbench lives in the Damage, Valuation, Estimate, Settlement and Report sections, always viewable and read-only once Complete; `/Cases/{id}/Assessment` is a 301 | [Case workspace](frd/frd-16-case-record-workspace.md#case-workspace) |
| UI-18 | Awaiting instruction (the Image-initiated Cases still awaiting an instruction) as a Pre-Case queue on Cases beside Triage | [Cases: queues and filters](frd/frd-15-work-centre-queues-and-search.md#cases-queues-and-filters) |
| UI-19 | Service health is Administration-only; Operations shows a one-line partial-data notice linking to it and carries no service health table | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| ENG-03 | Damage record: zones (front, left/right front, left/right side, left/right rear, rear, roof, four wheels, underside, interior, mechanical) each with severity, type and note; tyres and seat belts per corner, spare tyre, centre belt; unrelated damage with deduction; paint or material transfer; derived impact location and severity; the marked diagram printed on the report | [Damage record](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record) |
| ENG-04 | Settlement fields (outcome, category, salvage value, excess, betterment, claimant VAT registered, reserve, equity, repair duration and delays, report delay, storage per day, recovery, hire start and daily cost, diminution, salvage logistics) with equity derived and financial ratio lines permitted, not required | [Settlement](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#settlement) |
| AI-11 | `MarketResearch` AI job created from the Valuation section and completed outside Pegasus by the operator's external connector through the Automation Actor after research files are attached to the Case; optional source-labelled valuation entries remain proposals | [AI Job List](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list) |
| RPT-06 | Fee note preview on the Report section rendered from the agreed fee and description lines | [Report-draft entry point](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point) |
| DOC-01 | Automatic Box case-folder creation using the Case/PO name | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-02 | Store source emails, instruction documents, images, correspondence, and reports in Box | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-03 | Retained document versions | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-04 | Case document authorization and reversible Completed/Query handling | [Staging and custody](frd/frd-05-documents-extraction-and-custody.md#staging-and-custody) |
| DOC-05 | Logical file removal without destroying history | [Staging and custody](frd/frd-05-documents-extraction-and-custody.md#staging-and-custody) |
| DOC-07 | Staff upload, view, download, and export actions | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-08 | Private transient file staging for Worker processing | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| EXT-01 | DVLA/DVSA make, model, manufacture year, engine capacity, fuel type, MOT chronology, mileage evidence, and operator-confirmed reconciliation | [Vehicle data and MOT enrichment](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment) |
| EXT-02 | MOT chronology and mileage evidence with supplied-versus-external-versus-estimated classification | [Vehicle data and MOT enrichment](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment) |
| EXT-03 | Review-gated deterministic UTF-8 EVA export with the exact ordered 13-key JSON and every eligible retained Case-vehicle image; no EVA network call or Pegasus-owned image ordering | [Focused EVA manual handoff](frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff) |
| EXT-14 | Manual addition of relevant WhatsApp material | [Staging and custody](frd/frd-05-documents-extraction-and-custody.md#staging-and-custody) |
| EXT-18 | Operator-confirmed or staff-supplied physical vehicle/repairer address, or exact `Image Based Assessment` autofilled from the Principal's inspection-mode setting; no address is ever inferred from a provider or domain match | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| MCP-01 | Management/development-controlled MCP ingress for one named vendor-neutral Automation Actor through Pegasus Core use cases | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-02 | Automation Actor Case actions through the same Core use cases as the staff app | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-03 | Automation Actor intake-queue actions through the same Core use cases as the staff app | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-04 | Automation Actor document actions through the same Core use cases as the staff app | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-06 | Automation Actor assessment actions: direct writes with logging parity (assessment get/update and case-detail update) through the same Core use cases and guards as the staff app | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| OPS-01 | Production staff Web application on Azure | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-02 | Continuously running Worker for mailbox and background processing | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-03 | Azure SQL persistence | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-04 | Safe database migrations and concurrent reference allocation | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-05 | Managed identity and least-privilege RBAC between Azure services | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-06 | Infisical/Key Vault custody for unavoidable third-party secrets | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-07 | Correlated Web/Worker telemetry and dependency readiness checks | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-08 | Alerts for ingestion, processing, Box, matching, chasing, export, security, availability, and cost failures | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-09 | Database backup, restore proof, 15-minute RPO, and four-hour RTO | [Quality and recovery objectives](prd/pegasus-product.md#quality-capacity-security-and-evidence) |
| OPS-11 | Production isolated from local-development resources | [ADR-0014](adr/0014-local-to-production-deployment.md) |
| OPS-13 | Deployment preview, policy/quota checks, health probes, and smoke tests | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-14 | Production cutover and previous-artifact rollback procedure | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-20 | Capacity for about eight concurrent staff and 2,000 new cases per month | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-24 | Direct production deployment from an authorised terminal using committed Bicep through `azd` | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| DATA-01 | Publish immutable cumulative provider-domain reference snapshots from approved spreadsheets | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| OPS-23 | Operator acceptance against the real end-to-end workflow | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-25 | Collision Engineers management approval before production release | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| INT-31 | Authenticated staff generate a temporary, revocable, expiring, request-scoped link for isolated unauthenticated image/document upload; it exposes only the upload form and immediate result, never case/reference/request state or another document | [Request-scoped upload links](frd/frd-18-manual-upload-and-upload-links.md#request-scoped-upload-links) |
| DATA-02 | Prepare inspection-address / repairer reference data from separately approved spreadsheets | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| INT-04 | Activate additional providers through the shared intake/case workflow using separately accepted provider evidence and rules | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-05 | Automatic ingestion from `desk@collisionengineers.co.uk` | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-06 | Automatic ingestion from `engineers@collisionengineers.co.uk` | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-07 | Automatic ingestion from `info@collisionengineers.co.uk` | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-14 | Automated legacy DOC extraction | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-15 | Automated MSG extraction | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-16 | Qualified OCR for incoming scanned instructions | [Qualified OCR](frd/frd-05-documents-extraction-and-custody.md#qualified-ocr) |
| INT-28 | Automatic matching of image-led and instruction-led records | [Pairing and merge](frd/frd-19-image-led-intake-and-pairing.md#pairing-and-merge); [Grouped image-intake routing](frd/frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing) |
| MAIL-01 | Identify every inbound mailbox item and its mailbox/thread/message identity | [Inbound mailbox identity](frd/frd-08-email-mailbox-and-background-processing.md#inbound-mailbox-identity) |
| MAIL-02 | Map detailed email classifications to Receiving work, Queries, their named detailed views, reasoned Other, Unidentified, or the separate Triage workflow | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-03 | One shared classification policy across all supported mailboxes | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-04 | Explainable classification evidence, policy version, and correction history | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-05 | Recommend the designated Outlook folder for a classified message | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-06 | Staff confirmation of a recommended folder move in Pegasus | [Classification, linking and folder-move actions](frd/frd-20-mailbox-workspace.md#classification-linking-and-folder-move-actions) |
| MAIL-07 | Move the confirmed message to the designated Outlook folder | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-08 | Suggested next actions for classified email | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-09 | Automatic association of related email and attachments with a case | [Automatic Case association of retained mail](frd/frd-08-email-mailbox-and-background-processing.md#automatic-case-association-of-retained-mail) |
| MAIL-10 | Manual email/case association, unlink, relink, and correction | [Classification, linking and folder-move actions](frd/frd-20-mailbox-workspace.md#classification-linking-and-folder-move-actions) |
| MAIL-11 | Browse, search, and view mailbox messages and conversation threads in the app, including read-only search of accepted Deleted Items | [Quick preview and message detail](frd/frd-20-mailbox-workspace.md#quick-preview-and-message-detail) |
| MAIL-13 | Change read state, Outlook categories, flags, or delete messages in the app | [Classification, linking and folder-move actions](frd/frd-20-mailbox-workspace.md#classification-linking-and-folder-move-actions) |
| CASE-23 | Post-report query and dispute work on the existing case with retained report/reply-chain evidence and an explicit lifecycle | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| UI-10 | Full email-management workspace | [Inbox scopes and filters](frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters) |
| UI-14 | Categorised email views for Receiving work, Queries, Other, Unidentified, Triage, and named detailed classifications | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue); [Inbox scopes and filters](frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters) |
| API-01 | Principal-scoped provider submission API | [Accepted API-01 submission contract](frd/frd-09-provider-and-intermediary-routes.md#accepted-api-01-submission-contract) |
| API-02 | Provider API receipt and processing-status lookup | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| API-03 | Provider API resulting Case/PO lookup | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| API-04 | Provider API credential issue, reset, revoke, pause, and resume | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| MCP-05 | Automation Actor actions for the broader classified-email workspace | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| AI-05 | Automatic AI-assisted image readiness assessment of the current Case image set | [Ordinary-image VRM and image analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) |
| MAIL-23 | Map the detailed taxonomy to operational queues and designated Outlook folders | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| INT-32 | Instruction/image halves retain separate age and chase state; definitive pairing notifies staff that the job is ready | [Age and chase state](frd/frd-19-image-led-intake-and-pairing.md#age-and-chase-state) |
| INT-33 | Near-real-time durable email and manual-upload intake with truthful transient state and loss recovery | [Source occurrence and dispatch identity](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity); [Mailbox wake-up and recovery](frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-wake-up-and-recovery) |
| MAIL-19 | Automatically send chasers or other outbound messages | [Outbound correspondence](frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence) |
| CASE-05 | Diminution cases | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-06 | Commercial cases | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| EXT-15 | Automated WhatsApp ingestion and coexistence | [Intake and source identity](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| AI-01 | In-app staff AI assistant | [Requirements](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| AI-02 | AI-assisted email identification/classification | [Requirements](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| AI-03 | AI-assisted suggested email actions | [Requirements](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| AI-04 | AI-assisted document extraction and operator review | [Requirements](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| AI-06 | AI-assisted inspection-address suggestions | [Requirements](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MAIL-17 | Idempotent report/fee-note send on the original Outlook thread or provider API using principal CC/delivery/standing-note preferences, followed by Box filing, completion, and management-event recording | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| CASE-22 | Replace EVA inspection and report-preparation work inside Pegasus | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| EXT-04 | Principal-selected report generation: Pegasus, EVA ZIP export, manual EVA API submission, or automatic EVA API submission on entering Review | [EVA handoff routes](frd/frd-07-eva-and-external-engineering-handoff.md#eva-handoff-routes) |
| EXT-05 | Replace EVA Engineer assignment | [External boundary](frd/frd-07-eva-and-external-engineering-handoff.md#external-boundary) |
| EXT-06 | Replace EVA estimating without moving repair-specification authority out of Pegasus Core | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| EXT-07 | Replace EVA valuation while preserving separate dated/versioned source evidence and explicit Engineer selection | [External boundary](frd/frd-07-eva-and-external-engineering-handoff.md#external-boundary) |
| EXT-08 | Activate deterministic report generation from accepted Core-owned data through the approved renderer contract | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| EXT-09 | Versioned repair-estimate lines, source versions, and approvals | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| EXT-10 | Versioned vehicle-valuation evidence, explicit Engineer acceptance/adjustments/rationale, and revaluation history | [External boundary](frd/frd-07-eva-and-external-engineering-handoff.md#external-boundary) |
| EXT-11 | Versioned fee/invoice and Engineer cost/payment inputs, accounting status, and role-restricted visibility | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| EXT-12 | Glass's and Audatex PDF estimate ingestion with retained source and variant proof | [Retained PDF estimates](frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#retained-pdf-estimate-import) |
| EXT-13 | Independently licensed valuation-source adapters that preserve each source observation and version | [External boundary](frd/frd-07-eva-and-external-engineering-handoff.md#external-boundary) |
| AI-07 | Staff-selected `AI Assessor` Engineer option in the post-EVA-replacement assignment workflow; it owns no button, queue, model, or transport | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| MAIL-12 | Authenticated staff compose, reply, forward, and send email in Pegasus | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| EXT-16 | Collision Engineers guided mobile image capture | [Ordinary-image VRM and image analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) |
| EXT-17 | Tractable or Ravin guided-capture integration | [Ordinary-image VRM and image analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) |
| EXT-19 | Collision Engineers custom application domain | [External boundary](frd/frd-07-eva-and-external-engineering-handoff.md#external-boundary) |
| CASE-31 | One accepted structured case/engineering record is the source for every deterministic report, fee note, addendum, query document, invoice input, and statistic | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| ENG-01 | One canonical repair specification with route provenance for Glass's, Audatex PDF, or an approved AI proposal | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| ENG-02 | Engineer-owned final value/deductions, outcome, salvage category/value, and roadworthiness/reason drive derived figures and narratives without retyping | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| UI-15 | One case-centred progressive Engineer workbench for inspection, vehicle/damage, valuation, estimate/repairer, report, media, salvage, text, and administration | [Assessment](frd/frd-16-case-record-workspace.md#assessment) |
| RPT-01 | Deterministic renderer validates accepted data, computes each figure once, and applies the fixed Collision Engineers design | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| RPT-02 | Assessment rendering covers four outcome variants and emits the fee note plus itemised repair-specification breakdown | [Report-draft entry point](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point) |
| RPT-03 | Audit rendering reuses the Inspection report's approved physical output while retaining Audit reference provenance | [Audit report parity](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#audit-report-parity) |
| RPT-04 | Diminution rendering uses accepted original-case data plus the Engineer-entered percentage | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| RPT-05 | Addenda render from accepted case data plus a versioned amendment without retyping the case | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| RPT-07 | Estimate document rendered per estimate version from the one totals owner | [Report generation entry point](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point) |
| AI-08 | Intended Microsoft Foundry candidate proposes a case-grounded query response in approved house style/letterhead; a named Engineer reviews, amends if needed, and approves it before sending | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| AI-09 | Staff `Send to AI` creates one durable idempotent capability-scoped work request bound to an immutable case/version stamp; the hand-off carries a pointer only, and the scoped worker returns its work as attributed unconfirmed Automation Actor writes reviewed by the Engineer taking the work, with delivery status and visible failure on the tracking record | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| AI-10 | Extensible named AI job catalogue beginning with Case assessment, with durable lifecycle and an operator-visible AI Viewer for request state and eventual live work events | [AI Job List](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list) |
| MCP-07 | Administration-configurable Send to AI channel connector setup: base URL, token entry/rotation, and timeout configured from Administration, with connector health/status display, replacing the current configuration/user-secrets-only setup | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| MI-01 | Per-Engineer throughput, query rate/types, and Audit uplift | [Requirements](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-and-engineering-evidence) |
| MI-02 | Per-principal report counts, types, and periods feeding invoice generation | [Requirements](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-and-engineering-evidence) |
| MI-03 | Holding-pen age and instruction-to-images, ready-to-sent, and overall turnaround measures consuming accepted workflow events | [Requirements](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-and-engineering-evidence) |
| ACC-12 | External/customer application accounts | [Permanent boundaries](prd/pegasus-product.md#permanent-boundaries) |
| ACC-13 | Public registration | [Permanent boundaries](prd/pegasus-product.md#permanent-boundaries) |
| ACC-14 | Multi-factor authentication for staff | [Permanent boundaries](prd/pegasus-product.md#permanent-boundaries) |
| UI-12 | Responsive/mobile staff interface | [Operator experience](frd/frd-12-operator-experience.md#operator-experience) |
| DOC-09 | Automated malware scanning of inbound files | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-10 | Document redaction workflow | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-11 | Digital signatures | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-12 | Automated retention and deletion policy | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-13 | Legal-hold workflow | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-14 | Subject-access, correction, export, and erasure workflow | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-15 | Dedicated DPIA/compliance workflow | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| OPS-12 | GitHub Actions deployment using scoped OIDC identities | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-15 | Separate staging environment | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-16 | Deployment slots / Standard S1 hosting | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-17 | Private networking | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-18 | Zone redundancy | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-19 | Multi-region failover | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| OPS-21 | Quarterly restore/recovery exercise | [Requirements](frd/frd-12-operator-experience.md#operator-experience) |
| BND-01 | Import predecessor cases or application data | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-02 | Keep the predecessor application available after cutover | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-03 | Reuse predecessor application code | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-04 | SMS integration | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-05 | Microsoft Teams integration | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-06 | Persistent external case/customer portal; request-scoped upload links under INT-31 are permitted | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-07 | Independent Engineer accounts | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-08 | Solicitor, insurer, repairer, or vehicle-owner accounts | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-09 | Separate QA/test environment | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-10 | Separate user-acceptance environment | [Requirements](prd/pegasus-product.md#permanent-boundaries) |
| BND-11 | Training/demo environment | [Requirements](prd/pegasus-product.md#permanent-boundaries) |

## Retired and source identities

`DOC-06` is retired in favor of `INT-31` and is never reused. Earlier allocation
tables and delivery statements remain recoverable from Git history.

| Source ID | Requirement owner |
| --- | --- |
| CAP-001 | [FRD-08](frd/frd-08-email-mailbox-and-background-processing.md) |
| CAP-002 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |
| CAP-003 | [FRD-05](frd/frd-05-documents-extraction-and-custody.md) |
| CAP-004 | [FRD-08](frd/frd-08-email-mailbox-and-background-processing.md) |
| CAP-005 | [FRD-09](frd/frd-09-provider-and-intermediary-routes.md) |
| CAP-006 | [FRD-10](frd/frd-10-mcp-automation-and-actor-boundary.md) |
| CAP-007 | [FRD-06](frd/frd-06-vehicle-and-engineering-evidence.md) |
| CAP-008 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |
| CAP-009 | [FRD-09](frd/frd-09-provider-and-intermediary-routes.md) |
| CAP-010 | [FRD-07](frd/frd-07-eva-and-external-engineering-handoff.md) |
| CAP-011 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |
| CAP-012 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |
| CAP-013 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |
| CAP-014 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |
| CAP-015 | [FRD-10](frd/frd-10-mcp-automation-and-actor-boundary.md) |
| CAP-016 | [FRD-01](frd/frd-01-case-identity-and-lifecycle.md) |
| CAP-017 | [FRD-05](frd/frd-05-documents-extraction-and-custody.md) |
| CAP-018 | [FRD-06](frd/frd-06-vehicle-and-engineering-evidence.md) |
| CAP-019 | [FRD-06](frd/frd-06-vehicle-and-engineering-evidence.md) |
| CAP-020 | [FRD-06](frd/frd-06-vehicle-and-engineering-evidence.md) |
| CAP-021 | [FRD-08](frd/frd-08-email-mailbox-and-background-processing.md) |
| CAP-022 | [FRD-02](frd/frd-02-intake-and-source-identity.md) |

Source IDs preserve provenance; they are not duplicate delivery records.
