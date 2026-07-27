# Capability inventory

This is the stable product-capability inventory. Each capability keeps its
settled ID and durable outcome, has one canonical owner, and uses an exact
Semantic Version or `unallocated`. GitHub issues exist only when work is
activated; this table is not a backlog or implementation-status ledger.

The retained [feature maturity map](../history/plans/feature-maturity-map.md) and
[worksheet](../../FEATURE_VERSIONING.md) preserve the source wording and old
V0/V1/V2/V3 allocation evidence. Their labels no longer act as release
versions. Active timing uses `Now`, `Next`, `Later`, or `Not planned`; every
exact target release remains `unallocated` until release planning assigns it.
The 128 `Now` capabilities are assigned to `0.1.0-alpha.1` by the coupled
provider/intermediary-foundation and QDOS-alpha plan.

## Allocation summary

| Horizon | Capabilities | Meaning |
| --- | ---: | --- |
| Now | 128 | Current proof and QDOS alpha outcome; activate issues only for selected work. |
| Next | 30 | Additional-provider and email/API beta outcomes after named dependencies. |
| Later | 25 | Retained outcomes requiring their promotion conditions or direct decisions. |
| Not planned | 30 | Permanent product boundaries; no implementation issue or placeholder. |

Total: **213 capabilities; 213 unique IDs**.

## Capabilities

| ID | Durable outcome | Horizon | Target release | Canonical owner | Retained source allocation | Activation/boundary |
| --- | --- | --- | --- | --- | --- | --- |
| OPS-10 | Azure development/integration environment deployed directly from an authorised terminal | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| OPS-22 | Genuine-corpus local evaluation harness | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| EVAL-01 | Local development-only EML categorisation evaluator using `unchecked` and `checked` workspace folders | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| EVAL-02 | Reviewer selects from the detailed Received/Sent/Reply taxonomy and records required reasoning | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| EVAL-03 | `Other` category lets the reviewer enter a new category name and reasoning | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| EVAL-04 | Moving the reviewed workspace EML into `checked` records the human result | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| EVAL-05 | Display the rule-generated category and evidence beside the human review once rules exist | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| MAIL-20 | Run live provider-specific instruction-email categorisation against `.eml` files in the local folder-based evaluator | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| MAIL-21 | Minimum shared Core classification foundation: versioned rules, decision evidence, ambiguity outcome, and acceptance cohort | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| MAIL-22 | Detailed email taxonomy from `docs/reference/CollisionSPikeCurrenttree.txt`, including Received/Sent categories, subtypes, and mirrored Reply classifications | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V0 pre-alpha | Allocation only; owning evidence still required. |
| ACC-01 | Staff sign-in with CollisionSpike-managed usernames and passwords | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-02 | Administrator, Engineer, and User roles | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-03 | Staff account creation, disabling, access review, and role assignment | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-04 | Role-based protection for every non-public page and action | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-05 | Principal/provider administration | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-06 | Principal-code replacement with linked predecessor and sequence continuity | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-07 | Application and workflow configuration managed by Administrators | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-08 | Approved Outlook mailbox allowlist managed by Administrators | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-09 | Permanent action history for business changes, exports, and material failures | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-10 | Separate authentication/security log | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| ACC-11 | Operational telemetry (`content-safe` wording considered unnecessary) | Now | 0.1.0-alpha.1 | [Area](areas/identity-and-access.md) | V1 alpha gate | Required and accepted before V1. |
| INT-01 | Manual upload of instruction emails, documents, and vehicle images | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-02 | Automatic ingestion from `instructions@collisionengineers.co.uk` | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-03 | Correct handling of staff-forwarded email as real intake | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-08 | Stable source identity, duplicate-delivery handling, and idempotent retry | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-09 | Original inbound source and attachment custody | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-10 | EML and freehand email-body extraction | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-11 | PDF embedded-text and embedded-image extraction | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-12 | DOCX text and every visible image-placement extraction, without deduplicating repeated appearances | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-13 | JPEG and PNG image-led intake | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-17 | Automatic vehicle-registration reading from ordinary vehicle images | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-18 | Bounded, fail-closed processing for unreadable, oversized, or incomplete sources | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-19 | Typed, editable, operator-reviewable extracted case draft | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-20 | Field provenance, validation, missing-value, and contradiction display | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-21 | Human-reviewed extraction cohort, holdout, and field-level accuracy reporting | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-22 | Automatic identification of the correct principal/provider | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-23 | `Needs sorting` queue for uncertain or unsupported intake | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-24 | Manual `Blocked intake` filter with reason, warning, resolve, and retry | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-25 | Automatic case creation from definitive authorised intake | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-26 | Manual case creation through the same business rules | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-27 | Registration-based provisional identity for image-led work | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-29 | Manual linking and reasoned reversal of a mistaken match/merge | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| INT-30 | Preservation of original intake origin after linking or merging | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| MAIL-14 | Detect an exact Outlook Sent item as report-sent evidence | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| MAIL-15 | Manually link, unlink, or relink an exact Sent item with a reason | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| MAIL-16 | Automatically match the exact report Sent item to its case | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| MAIL-18 | Generate copyable chaser messages for staff to send manually | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-01 | Separate pre-case Triage record and workflow | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-02 | Vehicle-registration gate and `Needs sorting` fallback | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-03 | Open, Awaiting information, Finding recorded, Completed, and Cancelled states | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-04 | Roadworthy/Unroadworthy finding and reasoned replacement | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-05 | Exact reply-chain Sent-item evidence required for completion | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-06 | Reopen and superseding-finding behavior with permanent history | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-07 | Optional later case link, unlink, and relink | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-08 | Dedicated Triage list and detail workspace | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| TRI-09 | Optional Triage assignee, with no due date and no chasers | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-01 | Every active QDOS case type can travel end to end from intake through accepted case workflow to successful EVA export/handoff | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-02 | Inspection cases | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-03 | Standalone Audit cases | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-04 | Inspection + Audit cases and secondary Audit reference | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-07 | Shared principal/year case sequence | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-08 | Repairable `a.` and total-loss `ap.` Audit references | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-09 | Case principal and reference immutability after allocation | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-10 | Wrong-principal `Created in error` closure and linked replacement case | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-11 | Typed provider, claimant, claim, vehicle, accident, contact, and inspection data | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-12 | Relationships to staff/Engineer, repairer/bodyshop, insurer, and contacts | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-13 | Separate staff judgements for instruction completeness and image completeness | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-14 | Configurable completeness gate before Engineer assignment | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-15 | Configurable staff review gate before Engineer assignment | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-16 | `Not ready`, `Review`, and `Held` workflow | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-17 | Due-by date extraction and overdue display | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-18 | Seven-calendar-day missing-information chase schedule | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-19 | Hold/release behavior that preserves the chase interval | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-20 | General case tasks and reminders | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-21 | Successful EVA JSON/image export records the V1 `Sent to Engineer` handoff/proxy; EVA owns the actual named-Engineer assignment | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-24 | Post-report completion, provider cancellation, and Collision Engineers rejection outcomes | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-25 | Reasoned reopening into a valid nonterminal state | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-26 | Archive without permanent case deletion | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-27 | Exclusive edit lease and stale-write protection | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-28 | Roadworthiness and repairable/total-loss findings | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-29 | Inspection address or `Image Based Assessment` | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| CASE-30 | Track the V1 inspection/report stage and EVA handoff without replacing EVA's engineering workflow | Now | 0.1.0-alpha.1 | [Area](areas/intake-and-casework.md) | V1 alpha gate | Required and accepted before V1. |
| UI-01 | Operations dashboard/cockpit | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-02 | Case queues for Not ready, Review, and Held | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-03 | V1 intake queues for Needs sorting and Blocked intake | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-04 | In today, Sent to Engineer, and Reports sent day/week activity | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-05 | Click-through filtered work queues | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-06 | Last-updated time and manual refresh | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-07 | Search/filter by reference, registration, claimant, claim, principal, state, Engineer, dates, and origin | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-08 | Three-column intake review workbench | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-09 | Full case workspace | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-11 | Accounts, principals, mailbox allowlist, and configuration workspace | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| UI-13 | Accessible keyboard, screen-reader, focus, contrast, and error behavior | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-01 | Automatic Box case-folder creation using the Case/PO name | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | V1 gate; preserve test-folder scope. |
| DOC-02 | Store source emails, instruction documents, images, correspondence, and reports in Box | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-03 | Retained document versions | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-04 | Closed-case read-only files and reopen-before-edit behavior | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-05 | Logical file removal without destroying history | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-06 | Box file-request creation | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-07 | Staff upload, view, download, and export actions | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DOC-08 | Private transient file staging for Worker processing | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| EXT-01 | DVLA/DVSA vehicle lookup | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| EXT-02 | MOT history and mileage estimation | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| EXT-03 | Operator-approved structured JSON and image-bundle export to EVA | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| EXT-14 | Manual addition of relevant WhatsApp material | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| EXT-18 | Inspection-address mapping or prediction | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| MCP-01 | OAuth-authorised internal staff MCP, primarily for Claude Desktop | Now | 0.1.0-alpha.1 | [Area](areas/interfaces-and-automation.md) | V1 alpha gate | Required and accepted before V1. |
| MCP-02 | MCP case actions through the same Core use cases as the staff app | Now | 0.1.0-alpha.1 | [Area](areas/interfaces-and-automation.md) | V1 alpha gate | Required and accepted before V1. |
| MCP-03 | MCP intake-queue actions through the same Core use cases as the V1 staff app | Now | 0.1.0-alpha.1 | [Area](areas/interfaces-and-automation.md) | V1 alpha gate | Required and accepted before V1. |
| MCP-04 | MCP document actions through the same Core use cases as the staff app | Now | 0.1.0-alpha.1 | [Area](areas/interfaces-and-automation.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-01 | Production staff Web application on Azure | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-02 | Continuously running Worker for mailbox and background processing | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-03 | Azure SQL persistence | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-04 | Safe database migrations and concurrent reference allocation | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-05 | Managed identity and least-privilege RBAC between Azure services | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-06 | Infisical/Key Vault custody for unavoidable third-party secrets | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-07 | Correlated Web/Worker telemetry and dependency readiness checks | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-08 | Alerts for ingestion, processing, Box, matching, chasing, export, security, availability, and cost failures | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-09 | Database backup, restore proof, 15-minute RPO, and four-hour RTO | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-11 | Production isolated from local development and Azure development/integration resources | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-13 | Deployment preview, policy/quota checks, health probes, and smoke tests | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-14 | Production cutover and previous-artifact rollback procedure | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | V1 gate; implementation/recovery detail remains open. |
| OPS-20 | Capacity for about eight concurrent staff and 2,000 new cases per month | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| OPS-24 | Direct production deployment from an authorised terminal using committed Bicep through `azd` | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | V1 alpha gate | Required and accepted before V1. |
| DATA-01 | One-time preparation of provider reference data from spreadsheets | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| DATA-02 | One-time preparation of inspection-address / repairer reference data from spreadsheets | Now | 0.1.0-alpha.1 | [Area](areas/documents-and-integrations.md) | V1 alpha gate | Required and accepted before V1. |
| INT-04 | Activate additional providers during V1.x through the same intake/case workflow using bounded provider reference data and rules | Next | unallocated | [Area](areas/intake-and-casework.md) | V1.x before V2 | Additional providers before V2; shared workflow only. |
| OPS-23 | Operator acceptance against the real end-to-end workflow | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | Pre-V1 gate | Required before V1 acceptance. |
| OPS-25 | Collision Engineers management approval before production release | Now | 0.1.0-alpha.1 | [Area](areas/platform-and-operator-experience.md) | Pre-V1 gate | Required before V1 acceptance. |
| INT-05 | Automatic ingestion from `desk@collisionengineers.co.uk` | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| INT-06 | Automatic ingestion from `engineers@collisionengineers.co.uk` | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| INT-07 | Automatic ingestion from `info@collisionengineers.co.uk` | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| INT-14 | Automated legacy DOC extraction | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| INT-15 | Automated MSG extraction | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| INT-16 | OCR for scan-like PDF instruction pages | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| INT-28 | Automatic matching of image-led and instruction-led records | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-01 | Identify every inbound mailbox item and its mailbox/thread/message identity | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-02 | Map detailed email classifications to Receiving work, Query, Other, Needs sorting, or the separate Triage workflow | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-03 | One shared classification policy across all supported mailboxes | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-04 | Explainable classification evidence, policy version, and correction history | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-05 | Recommend the designated Outlook folder for a classified message | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-06 | Staff confirmation of a recommended folder move in CollisionSpike | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-07 | Move the confirmed message to the designated Outlook folder | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-08 | Suggested next actions for classified email | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-09 | Automatic association of related email and attachments with a case | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-10 | Manual email/case association, unlink, relink, and correction | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-11 | Browse, search, and view mailbox messages and conversation threads in the app | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-13 | Change read state, Outlook categories, flags, or delete messages in the app | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| CASE-23 | Post-report query and dispute work | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| UI-10 | Full email-management workspace | Next | unallocated | [Area](areas/platform-and-operator-experience.md) | V2 beta | Allocation only; owning evidence still required. |
| UI-14 | Categorised email queues for Receiving work, Queries, and Other | Next | unallocated | [Area](areas/platform-and-operator-experience.md) | V2 beta | Allocation only; owning evidence still required. |
| API-01 | Principal-scoped provider submission API | Next | unallocated | [Area](areas/interfaces-and-automation.md) | V2 beta | Allocation only; owning evidence still required. |
| API-02 | Provider API receipt and processing-status lookup | Next | unallocated | [Area](areas/interfaces-and-automation.md) | V2 beta | Allocation only; owning evidence still required. |
| API-03 | Provider API resulting Case/PO lookup | Next | unallocated | [Area](areas/interfaces-and-automation.md) | V2 beta | Allocation only; owning evidence still required. |
| API-04 | Provider API credential issue, rotation, and revocation | Next | unallocated | [Area](areas/interfaces-and-automation.md) | V2 beta | Allocation only; owning evidence still required. |
| MCP-05 | MCP actions for the broader classified-email workspace | Next | unallocated | [Area](areas/interfaces-and-automation.md) | V2 beta | Allocation only; owning evidence still required. |
| AI-05 | AI/vision assistance for vehicle images or damage evidence | Next | unallocated | [Area](areas/interfaces-and-automation.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-23 | Map the detailed taxonomy to operational queues and designated Outlook folders | Next | unallocated | [Area](areas/intake-and-casework.md) | V2 beta | Allocation only; owning evidence still required. |
| MAIL-19 | Automatically send chasers or other outbound messages | Later | unallocated | [Area](areas/intake-and-casework.md) | V3 release work | Allocation only; owning evidence still required. |
| CASE-05 | Diminution cases | Later | unallocated | [Area](areas/intake-and-casework.md) | V3 release work | Allocation only; owning evidence still required. |
| CASE-06 | Commercial cases | Later | unallocated | [Area](areas/intake-and-casework.md) | V3 release work | Allocation only; owning evidence still required. |
| EXT-15 | Automated WhatsApp ingestion and coexistence | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3 release work | Allocation only; owning evidence still required. |
| AI-01 | In-app staff AI assistant | Later | unallocated | [Area](areas/interfaces-and-automation.md) | V3 release work | Allocation only; owning evidence still required. |
| AI-02 | AI-assisted email identification/classification | Later | unallocated | [Area](areas/interfaces-and-automation.md) | V3 release work | Activate only if rule-based behavior is insufficient and approvals are met. |
| AI-03 | AI-assisted suggested email actions | Later | unallocated | [Area](areas/interfaces-and-automation.md) | V3 release work | Activate only if rule-based behavior is insufficient and approvals are met. |
| AI-04 | AI-assisted document extraction and operator review | Later | unallocated | [Area](areas/interfaces-and-automation.md) | V3 release work | Activate only if rule-based behavior is insufficient and approvals are met. |
| AI-06 | AI-assisted inspection-address suggestions | Later | unallocated | [Area](areas/interfaces-and-automation.md) | V3 release work | Activate only if rule-based behavior is insufficient and approvals are met. |
| MAIL-17 | Automatically send reports | Later | unallocated | [Area](areas/intake-and-casework.md) | V3+ release work | Allocation only; owning evidence still required. |
| CASE-22 | Replace EVA inspection and report-preparation work inside CollisionSpike | Later | unallocated | [Area](areas/intake-and-casework.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-04 | Direct EVA API integration | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Depends on usable EVA vendor capability and separate approval. |
| EXT-05 | Replace EVA Engineer assignment | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-06 | Replace EVA estimating | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-07 | Replace EVA valuation | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-08 | Replace EVA report generation | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-09 | Repair-estimate workflow | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-10 | Vehicle-valuation workflow | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-11 | Invoice amount and accounting/invoicing workflow | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-12 | Audatex or another estimating-service integration | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| EXT-13 | Other valuation-service integrations | Later | unallocated | [Area](areas/documents-and-integrations.md) | V3+ release work | Allocation only; owning evidence still required. |
| AI-07 | Staff-selected `AI Assessor` Engineer option in the post-EVA-replacement assignment workflow | Later | unallocated | [Area](areas/interfaces-and-automation.md) | V3+ release work | Allocation only; owning evidence still required. |
| ACC-12 | External/customer application accounts | Not planned | unallocated | [Area](areas/identity-and-access.md) | Never | Permanent boundary; not backlog. |
| ACC-13 | Public registration | Not planned | unallocated | [Area](areas/identity-and-access.md) | Never | Permanent boundary; not backlog. |
| ACC-14 | Multi-factor authentication for staff | Not planned | unallocated | [Area](areas/identity-and-access.md) | Never | Permanent boundary; not backlog. |
| MAIL-12 | Compose, reply, forward, and send email in the app | Not planned | unallocated | [Area](areas/intake-and-casework.md) | Never | Permanent boundary; not backlog. |
| UI-12 | Responsive/mobile staff interface | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| DOC-09 | Automated malware scanning of inbound files | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| DOC-10 | Document redaction workflow | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| DOC-11 | Digital signatures | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| DOC-12 | Automated retention and deletion policy | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| DOC-13 | Legal-hold workflow | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| DOC-14 | Subject-access, correction, export, and erasure workflow | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| DOC-15 | Dedicated DPIA/compliance workflow | Not planned | unallocated | [Area](areas/documents-and-integrations.md) | Never | Permanent boundary; not backlog. |
| OPS-12 | GitHub Actions deployment using scoped OIDC identities | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| OPS-15 | Separate staging environment | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| OPS-16 | Deployment slots / Standard S1 hosting | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| OPS-17 | Private networking | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| OPS-18 | Zone redundancy | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| OPS-19 | Multi-region failover | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| OPS-21 | Quarterly restore/recovery exercise | Not planned | unallocated | [Area](areas/platform-and-operator-experience.md) | Never | Permanent boundary; not backlog. |
| BND-01 | Import predecessor cases or application data | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-02 | Keep the predecessor application available after cutover | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-03 | Reuse predecessor application code | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-04 | SMS integration | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-05 | Microsoft Teams integration | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-06 | Customer/claimant portal | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-07 | Independent Engineer accounts | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-08 | Solicitor, insurer, repairer, or vehicle-owner accounts | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-09 | Separate QA/test environment | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-10 | Separate user-acceptance environment | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| BND-11 | Training/demo environment | Not planned | unallocated | [Area](boundaries.md) | Never | Permanent boundary; not backlog. |
| EXT-16 | Collision Engineers guided mobile image capture | Later | unallocated | [Area](areas/documents-and-integrations.md) | Conditional / Unclear | Unassigned; later direct decision required. |
| EXT-17 | Tractable or Ravin guided-capture integration | Later | unallocated | [Area](areas/documents-and-integrations.md) | Conditional / Unclear | Unassigned; later direct decision required. |
| EXT-19 | Collision Engineers custom application domain | Later | unallocated | [Area](areas/documents-and-integrations.md) | Conditional / Unclear | Unassigned; later direct decision required. |

## Allocation rules

- `Now` does not mean implemented; caller, local verification, deployment, live verification, and acceptance remain separate evidence states.
- `Next` and `Later` create no dormant service, schema, route, flag, credential, UI placeholder, or issue.
- `Not planned` is a product boundary, not a backlog state.
- Conditional capabilities remain `Later` only as retained problems; their direct decision and full planning route are promotion conditions.
- Changing an outcome, horizon, or release allocation requires the product authority, roadmap, and affected plan to change together.
