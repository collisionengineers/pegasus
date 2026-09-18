# Capability inventory

Stable capability identities link to their durable requirements. The owning
PRD/FRD/ADR defines scope and behavior; this registry does not allocate a release,
record delivery status or supply an independent acceptance roster. The current
operator task and linked PR/CI records own work ordering and delivery evidence.
A listed identity does not imply current activation or acceptance. A label is
a name, not a rule: the owner states the behaviour. `(deferred)` marks a
capability that is wanted and not built; `(excluded)` marks a permanent PRD
exclusion.

| ID | Capability | Owner |
| --- | --- | --- |
| OPS-10 | Production environment deployed directly from an authorised terminal | [ADR-0014](adr/0014-local-to-production-deployment.md) |
| OPS-22 | Genuine-corpus local evaluation harness | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-01 | Local development-only EML categorisation evaluator | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary); [ADR-0016](adr/0016-standalone-desktop-email-evaluator.md) |
| EVAL-02 | Reviewer selects from the detailed Received/Sent/Reply taxonomy and records required reasoning | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-03 | `Other` category lets the reviewer enter a new category name and reasoning | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| EVAL-04 | Evaluator copies the EML and appends a JSONL adjudication log | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary); [ADR-0016](adr/0016-standalone-desktop-email-evaluator.md) |
| EVAL-05 | Show the rule-generated category beside the human review | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| MAIL-20 | Run provider instruction-email categorisation in the local evaluator | [QDOS evaluation boundary](frd/frd-08-email-mailbox-and-background-processing.md#qdos-evaluation-boundary) |
| MAIL-21 | Shared Core classification: versioned rules, evidence, ambiguity outcome | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-22 | Detailed Received/Sent/Reply taxonomy with reasoned Other | [Settled mailbox taxonomy and correction](frd/frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction) |
| ACC-01 | Staff sign-in with Pegasus-managed usernames and passwords | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-02 | Administrator, Engineer and User roles held as data | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-03 | Staff account lifecycle, password reset, force logout, role assignment | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-04 | Role-based protection for every non-public page and action | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-05 | Contacts and Principal policy administration | [Contacts administration](frd/frd-04-parties-accounts-and-access.md#contacts-administration) |
| ACC-06 | Principal-code replacement with linked predecessor and sequence continuity | [Principal and case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| ACC-07 | Application and workflow configuration managed by Administrators | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-08 | Approved Outlook mailbox allowlist managed by Administrators | [Staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) |
| ACC-09 | Permanent action history for business changes, exports, and material failures | [Permanent action history](frd/frd-04-parties-accounts-and-access.md#permanent-action-history) |
| ACC-10 | Separate authentication/security log | [Permanent action history](frd/frd-04-parties-accounts-and-access.md#permanent-action-history) |
| ACC-15 | Administrator password reset with a revealed temporary password | [Staff accounts](frd/frd-04-parties-accounts-and-access.md#staff-accounts) |
| ACC-11 | Operational telemetry (`content-safe` wording considered unnecessary) | [Permanent action history](frd/frd-04-parties-accounts-and-access.md#permanent-action-history) |
| INT-01 | Manual upload of instruction emails, documents, and vehicle images | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
| INT-02 | Automatic ingestion from `instructions@collisionengineers.co.uk` | [Mailbox allowlist, activation and wipe](frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-allowlist-activation-and-wipe) |
| INT-03 | Correct handling of staff-forwarded email as real intake | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
| INT-08 | Stable source identity, duplicate-delivery handling, and idempotent retry | [Source occurrence and dispatch identity](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity) |
| INT-09 | Original inbound source and attachment custody | [Source occurrence and dispatch identity](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity) |
| INT-10 | EML and freehand email-body extraction | [Supported source boundary](frd/frd-05-documents-extraction-and-custody.md#supported-source-boundary) |
| INT-11 | PDF embedded-text and embedded-image extraction | [Supported source boundary](frd/frd-05-documents-extraction-and-custody.md#supported-source-boundary) |
| INT-12 | DOCX text and every visible image-placement extraction, without deduplicating repeated appearances | [Supported source boundary](frd/frd-05-documents-extraction-and-custody.md#supported-source-boundary) |
| INT-13 | JPEG and PNG image-led intake | [Grouped image-intake routing](frd/frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing) |
| INT-17 | Automatic vehicle-registration reading from ordinary vehicle images | [Ordinary-image VRM and image analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) |
| INT-18 | Bounded, fail-closed processing for unreadable, oversized, or incomplete sources | [Requirements](frd/frd-02-intake-and-source-identity.md#intake-and-source-identity) |
| INT-19 | Typed, editable, operator-reviewable extracted case draft | [Field provenance and value kinds](frd/frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds); [Upload confirmation surface](frd/frd-18-manual-upload.md#upload-confirmation-surface) |
| INT-20 | Field provenance, validation, missing-value, and contradiction display | [Field provenance and value kinds](frd/frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds) |
| INT-22 | Automatic identification of the correct principal/provider | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| INT-23 | Unidentified queue with immutable U-references | [Unidentified destination and reference](frd/frd-02-intake-and-source-identity.md#unidentified-destination-and-reference) |
| INT-24 | Unidentified Close, Reopen, Could not be read and retry actions | [Mandatory pre-case gates](frd/frd-22-pre-case-gates-matching-and-association.md#mandatory-pre-case-gates) |
| INT-25 | Automatic case creation from definitive authorised intake | [Matching conflicts and reversible association](frd/frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association) |
| INT-26 | Manual case creation through the same business rules | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
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
| TRI-04 | Roadworthiness and Assessment findings, each optional, corrected by superseding | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-05 | Outcome-based completion and optional Reply with outcome correspondence | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-06 | Reopen and superseding-finding behavior with permanent history | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-07 | Optional later case link, unlink, and relink | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| TRI-08 | Dedicated Triage list and detail workspace | [Pre-Case records](frd/frd-15-work-centre-queues-and-search.md#pre-case-records) |
| TRI-09 | Optional Triage assignee, with no due date and no chasers | [Triage normal workflow](frd/frd-03-triage.md#normal-workflow-and-completion-evidence) |
| CASE-01 | Every active Case type travels end to end to handoff | [States and labels](frd/frd-13-case-lifecycle-and-workflow.md#states-and-labels) |
| CASE-02 | Inspection cases | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-03 | Standalone Audit Case with or without the original report | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-04 | Inspection + Audit: linked Audit Case made by Create audit | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-07 | Shared principal/year case sequence | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-08 | One `a.` Audit reference prefix, independent of assessment outcome | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-09 | Case principal and reference immutability after allocation | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-10 | Wrong-principal `Created in error` closure and linked replacement case | [Principal, reference, and Case/PO identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-11 | Typed provider, claimant, claim, vehicle, accident, contact, and inspection data | [Case types and accepted Case projection](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-12 | Relationships to Engineer, repairer and contacts | [Principal and historical case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) |
| CASE-13 | Instruction and image completeness as separate named blockers | [Readiness and Review](frd/frd-13-case-lifecycle-and-workflow.md#readiness-and-review) |
| CASE-14 | Mandatory instruction-completeness and image-completeness gate before Engineers-queue eligibility | [Readiness and Review](frd/frd-13-case-lifecycle-and-workflow.md#readiness-and-review) |
| CASE-15 | Not ready becomes Review automatically when requirements are met | [Readiness and Review](frd/frd-13-case-lifecycle-and-workflow.md#readiness-and-review) |
| CASE-16 | `Not ready`, `Review`, `With Engineer`, `Completed`, `Query` and `Held` workflow | [States and labels](frd/frd-13-case-lifecycle-and-workflow.md#states-and-labels) |
| CASE-17 | Due-by date extraction and overdue display | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-18 | Configurable whole-calendar-day missing-information chase schedule | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-19 | Hold/release behavior that preserves the chase interval | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-20 | General Case tasks (back end only, no screen; deferred) | [Due work and chasing](frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) |
| CASE-21 | Replay-safe EVA export history and First sent to Engineer | [Focused EVA manual handoff](frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff) |
| CASE-24 | Post-report completion, provider cancellation, and Collision Engineers rejection outcomes | [Close case](frd/frd-13-case-lifecycle-and-workflow.md#close-case) |
| CASE-25 | Reasoned return to engineering through normal destination gates | [Actions](frd/frd-13-case-lifecycle-and-workflow.md#actions) |
| CASE-26 | Archive without permanent case deletion | [Archive](frd/frd-13-case-lifecycle-and-workflow.md#archive) |
| CASE-27 | Exclusive edit lease and stale-write protection | [Case edit lease](frd/frd-14-record-edit-leases.md#case-edit-lease) |
| CASE-28 | Independent Roadworthiness and Assessment findings with reasoned correction | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| CASE-29 | Principal inspection mode autofills Image Based Assessment or requires an address | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| CASE-30 | Track native inspection/report work with optional EVA handoff | [Focused EVA manual handoff](frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff) |
| CASE-32 | Sign-off Engineer field, default and signature tuple | [Sign-off Engineer](frd/frd-13-case-lifecycle-and-workflow.md#sign-off-engineer); [Staff accounts](frd/frd-04-parties-accounts-and-access.md#staff-accounts) |
| CASE-34 | Inspect at fast-update choices and Case storage location | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| UI-01 | Operations dashboard/cockpit | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| UI-02 | Case queues for Not ready, Review, and Held | [Cases: queues and filters](frd/frd-15-work-centre-queues-and-search.md#cases-queues-and-filters) |
| UI-03 | E-mail activity for Unidentified, including closed and could-not-be-read items | [Pre-Case records](frd/frd-15-work-centre-queues-and-search.md#pre-case-records) |
| UI-04 | New cases today, Sent to Engineer, and Reports sent day/week activity | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| UI-05 | Click-through filtered work queues | [Work Centre](frd/frd-15-work-centre-queues-and-search.md#work-centre) |
| UI-06 | Last-good time, distinct current/stale/partial/unavailable/failed states, auditable reconciliation, and manual refresh | [Dashboard freshness and reconciliation](frd/frd-15-work-centre-queues-and-search.md#dashboard-freshness-and-reconciliation) |
| UI-07 | Search and filter across Cases and pre-Case records | [Search](frd/frd-15-work-centre-queues-and-search.md#search) |
| UI-08 | Upload confirmation surface | [Upload confirmation surface](frd/frd-18-manual-upload.md#upload-confirmation-surface) |
| UI-09 | Full case workspace | [Case workspace](frd/frd-16-case-record-workspace.md#case-workspace) |
| UI-11 | Accounts, Contacts, mailbox allowlist, and configuration workspace | [Administration](frd/frd-17-administration-workspace.md#administration) |
| UI-13 | Accessible keyboard, screen-reader, focus, contrast, and error behavior | [Operator experience](frd/frd-12-operator-experience.md#operator-experience) |
| UI-16 | Operations Workspace shell: rail, counts, working set, command palette | [Shell and routes](frd/frd-12-operator-experience.md#shell-and-routes) |
| UI-17 | Case record: Scroll and Tabs modes over ten sections | [Case workspace](frd/frd-16-case-record-workspace.md#case-workspace) |
| UI-18 | Awaiting instruction pre-Case queue beside Triage | [Cases: queues and filters](frd/frd-15-work-centre-queues-and-search.md#cases-queues-and-filters) |
| UI-19 | Service health is Administration-only; Operations links to it | [Operations](frd/frd-15-work-centre-queues-and-search.md#operations) |
| ENG-03 | Damage record: zones with severity and note, tyres, belts, diagram | [Damage record](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record) |
| ENG-04 | Settlement fields with derived equity | [Settlement](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#settlement) |
| AI-11 | Market Research AI job completed outside Pegasus | [AI Job List](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list) |
| RPT-06 | Fee note preview on the Report section | [Report-draft entry point](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point) |
| DOC-01 | Automatic Box case-folder creation using the Case/PO name | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-02 | Store source emails, instruction documents, images, correspondence, and reports in Box | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-03 | Retained document versions | [Custody and derived reads](frd/frd-05-documents-extraction-and-custody.md#custody-and-derived-reads) |
| DOC-04 | Case document authorization and reversible Completed/Query handling | [Staging and custody](frd/frd-05-documents-extraction-and-custody.md#staging-and-custody) |
| DOC-05 | Logical file removal without destroying history | [Custody and derived reads](frd/frd-05-documents-extraction-and-custody.md#custody-and-derived-reads) |
| DOC-07 | Staff upload, view, download, and export actions | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| DOC-08 | Private transient file staging for Worker processing | [Requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody) |
| EXT-01 | DVLA/DVSA vehicle data, MOT chronology and confirmed reconciliation | [Vehicle data and MOT enrichment](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment) |
| EXT-02 | MOT chronology and mileage evidence with supplied-versus-external-versus-estimated classification | [Vehicle data and MOT enrichment](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment) |
| EXT-03 | Deterministic EVA export: ordered 13-key JSON and eligible images | [Focused EVA manual handoff](frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff) |
| EXT-14 | Manual addition of relevant WhatsApp material | [Staging and custody](frd/frd-05-documents-extraction-and-custody.md#staging-and-custody) |
| EXT-18 | Operator-confirmed inspection address, never inferred | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| MCP-01 | Controlled MCP ingress for one named Automation Actor | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-02 | Automation Actor Case actions through Core use cases | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-03 | Automation Actor intake-queue actions through Core use cases | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-04 | Automation Actor document actions through Core use cases | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| MCP-06 | Automation Actor assessment writes with logging parity | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| OPS-01 | Production staff Web application on Azure | [ADR-0049](adr/0049-host-web-on-app-service-code-deploy.md) |
| OPS-02 | Continuously running Worker for mailbox and background processing | [ADR-0002 runtime architecture](adr/0002-dotnet-modular-monolith-on-azure.md#runtime-architecture) |
| OPS-03 | Azure SQL persistence | [ADR-0002 data ownership](adr/0002-dotnet-modular-monolith-on-azure.md#data-ownership-and-consistency) |
| OPS-04 | Safe database migrations and concurrent reference allocation | [ADR-0002 deployment and database changes](adr/0002-dotnet-modular-monolith-on-azure.md#deployment-and-database-changes); [Case reference allocation](adr/0002-dotnet-modular-monolith-on-azure.md#case-reference-allocation) |
| OPS-05 | Managed identity and least-privilege RBAC between Azure services | [ADR-0002 secrets and Azure identities](adr/0002-dotnet-modular-monolith-on-azure.md#secrets-and-azure-identities) |
| OPS-06 | Infisical/Key Vault custody for unavoidable third-party secrets | [ADR-0002 secrets and Azure identities](adr/0002-dotnet-modular-monolith-on-azure.md#secrets-and-azure-identities) |
| OPS-07 | Correlated Web/Worker telemetry and dependency readiness checks | [ADR-0002 monitoring and recovery](adr/0002-dotnet-modular-monolith-on-azure.md#monitoring-and-recovery) |
| OPS-08 | Alerts for ingestion, processing, Box, export, security and cost | [ADR-0002 monitoring and recovery](adr/0002-dotnet-modular-monolith-on-azure.md#monitoring-and-recovery) |
| OPS-09 | Database backup, restore proof, 15-minute RPO, and four-hour RTO (deferred) | [Quality and recovery objectives](prd/pegasus-product.md#quality-capacity-security-and-evidence) |
| OPS-11 | Production isolated from local-development resources | [ADR-0014](adr/0014-local-to-production-deployment.md) |
| OPS-13 | Deployment preview, policy/quota checks, health probes, and smoke tests | [ADR-0007 decision](adr/0007-direct-terminal-azure-deployment.md#decision) |
| OPS-14 | Production cutover and previous-artifact rollback procedure | [ADR-0007 decision](adr/0007-direct-terminal-azure-deployment.md#decision) |
| OPS-20 | Capacity for about eight concurrent staff and 2,000 new cases per month | [ADR-0002 context](adr/0002-dotnet-modular-monolith-on-azure.md#context) |
| OPS-24 | Direct production deployment from an authorised terminal using committed Bicep through `azd` | [ADR-0007 decision](adr/0007-direct-terminal-azure-deployment.md#decision) |
| DATA-01 | Publish immutable cumulative provider-domain reference snapshots from approved spreadsheets | [Provider-domain reference authoring](runbook.md#provider-domain-reference-authoring) |
| OPS-23 | Operator acceptance against the real end-to-end workflow | [Acceptance model](prd/pegasus-product.md#acceptance-model) |
| OPS-25 | Collision Engineers management approval before production release | [Acceptance model](prd/pegasus-product.md#acceptance-model) |
| DATA-02 | Prepare inspection-address / repairer reference data from separately approved spreadsheets | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| INT-04 | Activate additional providers through the shared workflow | [Provider and intermediary routes](frd/frd-09-provider-and-intermediary-routes.md#provider-and-intermediary-routes) |
| INT-05 | Automatic ingestion from `desk@collisionengineers.co.uk` | [Mailbox allowlist, activation and wipe](frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-allowlist-activation-and-wipe) |
| INT-06 | Automatic ingestion from `engineers@collisionengineers.co.uk` | [Mailbox allowlist, activation and wipe](frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-allowlist-activation-and-wipe) |
| INT-07 | Automatic ingestion from `info@collisionengineers.co.uk` | [Mailbox allowlist, activation and wipe](frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-allowlist-activation-and-wipe) |
| INT-14 | Automated legacy DOC extraction | [Supported source boundary](frd/frd-05-documents-extraction-and-custody.md#supported-source-boundary) |
| INT-15 | Automated MSG extraction | [Supported source boundary](frd/frd-05-documents-extraction-and-custody.md#supported-source-boundary) |
| INT-16 | Qualified OCR for incoming scanned instructions | [Qualified OCR](frd/frd-05-documents-extraction-and-custody.md#qualified-ocr) |
| INT-28 | Automatic matching of image-led and instruction-led records | [Pairing and merge](frd/frd-19-image-led-intake-and-pairing.md#pairing-and-merge); [Grouped image-intake routing](frd/frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing) |
| MAIL-01 | Identify every inbound mailbox item and its mailbox/thread/message identity | [Inbound mailbox identity](frd/frd-08-email-mailbox-and-background-processing.md#inbound-mailbox-identity) |
| MAIL-02 | Map classifications to queues, Other, Unidentified or Triage | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-03 | One shared classification policy across all supported mailboxes | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-04 | Explainable classification evidence, policy version, and correction history | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-05 | Recommend the designated Outlook folder for a classified message | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-06 | Staff confirmation of a recommended folder move in Pegasus | [Classification, linking and folder-move actions](frd/frd-20-mailbox-workspace.md#classification-linking-and-folder-move-actions) |
| MAIL-07 | Move the confirmed message to the designated Outlook folder | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| MAIL-09 | Automatic association of related email and attachments with a case | [Automatic Case association of retained mail](frd/frd-08-email-mailbox-and-background-processing.md#automatic-case-association-of-retained-mail) |
| MAIL-10 | Manual email/case association, unlink, relink, and correction | [Classification, linking and folder-move actions](frd/frd-20-mailbox-workspace.md#classification-linking-and-folder-move-actions) |
| MAIL-11 | Browse, search and view mailbox messages and threads | [Quick preview and message detail](frd/frd-20-mailbox-workspace.md#quick-preview-and-message-detail) |
| MAIL-13 | Outlook category by allowlisted identifier; no read-state, flag or delete | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| CASE-23 | Post-report query and dispute work on the existing Case | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| UI-10 | Full email-management workspace | [Inbox scopes and filters](frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters) |
| UI-14 | Categorised email views by destination and classification | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue); [Inbox scopes and filters](frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters) |
| API-01 | Principal-scoped provider submission API | [Accepted API-01 submission contract](frd/frd-09-provider-and-intermediary-routes.md#accepted-api-01-submission-contract) |
| API-02 | Provider API receipt and processing-status lookup | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| API-03 | Provider API resulting Case/PO lookup | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| API-04 | Provider API credential issue, reset, revoke, pause, and resume | [Provider API principal and contract boundary](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary) |
| MCP-05 | Automation Actor actions for the broader classified-email workspace | [MCP automation and actor boundary](frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary) |
| AI-05 | Automatic AI-assisted image readiness assessment of the current Case image set | [Ordinary-image VRM and image analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) |
| MAIL-23 | Map the detailed taxonomy to operational queues and designated Outlook folders | [Classification, destination, and folder catalogue](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue) |
| INT-32 | Separate chase state per half; pairing shown in Work Centre (not built) | [Age and chase state](frd/frd-19-image-led-intake-and-pairing.md#age-and-chase-state); [Pairing and merge](frd/frd-19-image-led-intake-and-pairing.md#pairing-and-merge) |
| INT-33 | Near-real-time durable intake with truthful transient state | [Source occurrence and dispatch identity](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity); [Mailbox wake-up and recovery](frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-wake-up-and-recovery) |
| MAIL-19 | Automatically send chasers or other outbound messages | [Outbound correspondence](frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence) |
| CASE-05 | Diminution cases (deferred) | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| CASE-06 | Commercial cases (deferred) | [Case types](frd/frd-01-case-identity-and-lifecycle.md#case-types) |
| EXT-15 | Automated WhatsApp ingestion and coexistence (deferred) | [Operating and commercial constraints](prd/pegasus-product.md#operating-and-commercial-constraints) |
| AI-04 | AI-assisted document extraction and operator review | [Qualified OCR](frd/frd-05-documents-extraction-and-custody.md#qualified-ocr) |
| AI-06 | Ranked inspection-address suggestions, never chosen automatically | [Inspection address](frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address) |
| MAIL-17 | Idempotent report and fee-note send with Box filing | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| CASE-22 | Replace EVA inspection and report-preparation work inside Pegasus | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| EXT-04 | Principal-selected report route: Pegasus, EVA ZIP or EVA API | [EVA handoff routes](frd/frd-07-eva-and-external-engineering-handoff.md#eva-handoff-routes) |
| EXT-05 | Replace EVA Engineer assignment | [Hand to Engineer](frd/frd-13-case-lifecycle-and-workflow.md#hand-to-engineer) |
| EXT-06 | Replace EVA estimating without moving repair-specification authority out of Pegasus Core | [Canonical repair specifications](frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#canonical-repair-specifications) |
| EXT-07 | Dated valuation source evidence with explicit human staff selection | [Valuation sources](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources) |
| EXT-08 | Deterministic report generation from accepted Core data | [Initial renderer activation](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation) |
| EXT-09 | Versioned repair-estimate lines, source versions, and approvals | [Canonical repair specifications](frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#canonical-repair-specifications) |
| EXT-10 | Versioned vehicle-valuation evidence, explicit human staff acceptance/adjustments/rationale, and revaluation history | [Valuation sources](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources) |
| EXT-11 | Versioned fee/invoice and Engineer cost/payment inputs, accounting status, and staff-role-neutral visibility | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| EXT-12 | Glass's and Audatex PDF estimate ingestion with retained source and variant proof | [Retained PDF estimates](frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#retained-pdf-estimate-import) |
| EXT-13 | Independently licensed valuation-source adapters that preserve each source observation and version | [Valuation sources](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources) |
| AI-07 | AI Assessor as a staff-selected Engineer option | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| MAIL-12 | Authenticated staff compose, reply, forward, and send email in Pegasus | [Outbound correspondence](frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence) |
| EXT-17 | Tractable capture outside Pegasus, received as an emailed PDF | [Ways intake starts](frd/frd-02-intake-and-source-identity.md#ways-intake-starts) |
| CASE-31 | One accepted record feeds every report and fee note | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| ENG-01 | One canonical repair specification with route provenance | [Canonical repair specifications](frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#canonical-repair-specifications) |
| ENG-02 | Human staff-owned value, outcome and salvage drive derived figures | [Professional engineering findings and correction](frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction) |
| UI-15 | Engineer workbench inside the Case record sections | [Assessment](frd/frd-16-case-record-workspace.md#assessment) |
| RPT-01 | Renderer validates data, computes figures once, fixed design | [Initial renderer activation](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation) |
| RPT-02 | Four assessment outcomes with fee note and repair breakdown | [Assessment-report outcomes](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes) |
| RPT-03 | Audit report reuses the Inspection output with Audit provenance | [Audit report parity](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#audit-report-parity) |
| RPT-04 | Diminution rendering uses accepted original-case data plus the Engineer-entered percentage (deferred) | [Initial renderer activation](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation) |
| RPT-05 | Addenda from accepted data plus a versioned amendment (deferred) | [Report correction, finality, and post-report work](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-correction-finality-and-post-report-work) |
| RPT-07 | Estimate document rendered per estimate version from the one totals owner | [Report generation entry point](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point) |
| AI-08 | AI-drafted query response reviewed by human staff before sending | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| AI-09 | Send to AI: pointer-only hand-off, attributed unconfirmed writes | [Targeted sending and reviewed AI proposals](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#targeted-sending-and-reviewed-ai-proposals) |
| AI-10 | Named AI job kinds with durable lifecycle and job list | [AI Job List](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list) |
| MCP-07 | Administration settings for the Send to AI connector | [Send to AI connector settings](frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#send-to-ai-connector-settings) |
| MI-01 | Per-Engineer throughput, query rate/types, and Audit uplift | [Reports](frd/frd-17-administration-workspace.md#reports) |
| MI-02 | Per-principal report counts, types, and periods feeding invoice generation | [Reports](frd/frd-17-administration-workspace.md#reports) |
| MI-03 | Holding age and instruction-to-produced, ready and sent turnaround | [Reports](frd/frd-17-administration-workspace.md#reports) |
| ACC-12 | External/customer application accounts (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| ACC-13 | Public registration (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| ACC-14 | Multi-factor authentication for staff (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| UI-12 | Responsive/mobile staff interface (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-09 | Automated malware scanning of inbound files (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-10 | Document redaction workflow (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-11 | Digital signatures (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-12 | Automated retention and deletion policy (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-13 | Legal-hold workflow (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-14 | Subject-access, correction, export, and erasure workflow (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| DOC-15 | Dedicated DPIA/compliance workflow (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-12 | GitHub Actions deployment using scoped OIDC identities (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-15 | Separate staging environment (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-16 | Deployment slots / Standard S1 hosting (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-17 | Private networking (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-18 | Zone redundancy (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-19 | Multi-region failover (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| OPS-21 | Quarterly restore/recovery exercise (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-01 | Import predecessor cases or application data (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-02 | Keep the predecessor application available after cutover (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-03 | Reuse predecessor application code (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-04 | SMS integration (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-05 | Microsoft Teams integration (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-06 | Persistent external case/customer portal (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-07 | Independent Engineer accounts (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-08 | Solicitor, insurer, repairer, or vehicle-owner accounts (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-09 | Separate QA/test environment (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-10 | Separate user-acceptance environment (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |
| BND-11 | Training/demo environment (excluded) | [Excluded product capabilities](prd/pegasus-product.md#excluded-product-capabilities) |

## Retired identities

A retired identity is never reused. Earlier allocation tables, delivery
statements and the `CAP-0NN` source map remain recoverable from Git history.

| ID | Was | Retired |
| --- | --- | --- |
| DOC-06 | Replaced by `INT-31`, itself retired | before 2026-09 |
| INT-31 | Temporary, revocable, request-scoped upload links | 2026-09-17, operator decision |
| AI-01 | In-app staff AI assistant | 2026-09-18, operator decision |
| AI-02 | AI-assisted email classification | 2026-09-18, operator decision |
| AI-03 | AI-assisted suggested email actions | 2026-09-18, operator decision |
| MAIL-08 | Suggested next actions for classified email | 2026-09-18, operator decision |
| EXT-16 | Collision Engineers guided mobile image capture | 2026-09-18, operator decision |
| EXT-19 | Custom application domain | 2026-09-18, operator decision |
| INT-21 | Human-reviewed extraction cohort and accuracy reporting | 2026-09-18, operator decision |
