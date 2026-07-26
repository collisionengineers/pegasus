# CollisionSpike feature maturity map

Status: **Active canonical allocation map**

This map records allocation, not implementation status. `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted` remain distinct. Exact feature labels and trimmed answers come from the retained [worksheet](../../FEATURE_VERSIONING.md). Its latest clear direct answers govern older maturity labels; conditional, relative, blank or internally contradictory wording remains visible rather than being silently normalised.

Normalized allocations appear beside, never instead of, raw answers. Conditional, relative, and `Unclear` wording remains an activation constraint. `Never` is permanent, not deferred. Every row has exactly one primary planning destination; secondary dependencies belong in the linked plan and the [delivery roadmap](delivery-roadmap.md), not in this allocation cell.

## Allocation counts

| Allocation | Rows |
| --- | ---: |
| V0 pre-alpha | 10 |
| V1 alpha gate | 116 |
| V1.x before V2 | 1 |
| Pre-V1 gate | 2 |
| V2 beta | 29 |
| V3 release work | 9 |
| V3+ release work | 13 |
| Never | 30 |
| Conditional / Unclear | 3 |

Total: **213 rows; 213 unique IDs**.

## Primary planning ownership

The `Owning requirement/plan` cell is the single primary planning owner for that feature row. It must resolve to the bounded section that sequences and accepts the feature, or to the non-implementation boundary section for `Never` and conditional/Unclear work. A link is planning traceability only; it is not implementation, caller, deployment or acceptance evidence.

## V0 pre-alpha

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| OPS-10 | Azure development/integration environment deployed directly from an authorised terminal | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#provision-and-prove-v0-shared-development) | Allocation only; owning evidence still required. |
| OPS-22 | Genuine-corpus local evaluation harness | v0 | V0 pre-alpha | [Worksheet] | [Primary plan](long-term-local-testing/platform/local-testing.md#caller-backed-local-and-live-evidence-gates) | Allocation only; owning evidence still required. |
| EVAL-01 | Local development-only EML categorisation evaluator using `unchecked` and `checked` workspace folders | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#review-local-eml-workspace) | Allocation only; owning evidence still required. |
| EVAL-02 | Reviewer selects from the detailed Received/Sent/Reply taxonomy and records required reasoning | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#review-local-eml-workspace) | Allocation only; owning evidence still required. |
| EVAL-03 | `Other` category lets the reviewer enter a new category name and reasoning | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#review-local-eml-workspace) | Allocation only; owning evidence still required. |
| EVAL-04 | Moving the reviewed workspace EML into `checked` records the human result | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#review-local-eml-workspace) | Allocation only; owning evidence still required. |
| EVAL-05 | Display the rule-generated category and evidence beside the human review once rules exist | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#compare-versioned-policy-with-human-results) | Allocation only; owning evidence still required. |
| MAIL-20 | Run live provider-specific instruction-email categorisation against `.eml` files in the local folder-based evaluator | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#prove-the-core-classification-policy) | Allocation only; owning evidence still required. |
| MAIL-21 | Minimum shared Core classification foundation: versioned rules, decision evidence, ambiguity outcome, and acceptance cohort | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#prove-the-core-classification-policy) | Allocation only; owning evidence still required. |
| MAIL-22 | Detailed email taxonomy from `docs/reference/CollisionSPikeCurrenttree.txt`, including Received/Sent categories, subtypes, and mirrored Reply classifications | V0 | V0 pre-alpha | [Worksheet] | [Primary plan](mailbox-categorisation-and-email-matching/v0-classification-foundation-and-evaluator.md#prove-the-core-classification-policy) | Allocation only; owning evidence still required. |

## V1 alpha gate

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| ACC-01 | Staff sign-in with CollisionSpike-managed usernames and passwords | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Required and accepted before V1. |
| ACC-02 | Administrator, Engineer, and User roles | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Required and accepted before V1. |
| ACC-03 | Staff account creation, disabling, access review, and role assignment | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Required and accepted before V1. |
| ACC-04 | Role-based protection for every non-public page and action | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Required and accepted before V1. |
| ACC-05 | Principal/provider administration | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) | Required and accepted before V1. |
| ACC-06 | Principal-code replacement with linked predecessor and sequence continuity | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#replace-a-used-principal-code-through-an-immutable-cutover) | Required and accepted before V1. |
| ACC-07 | Application and workflow configuration managed by Administrators | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) | Required and accepted before V1. |
| ACC-08 | Approved Outlook mailbox allowlist managed by Administrators | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) | Required and accepted before V1. |
| ACC-09 | Permanent action history for business changes, exports, and material failures | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#attribute-permanent-action-history-and-automation) | Required and accepted before V1. |
| ACC-10 | Separate authentication/security log | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Required and accepted before V1. |
| ACC-11 | Operational telemetry (`content-safe` wording considered unnecessary) | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#prove-persistence-observability-and-recovery-in-shared-development) | Required and accepted before V1. |
| INT-01 | Manual upload of instruction emails, documents, and vehicle images | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-02 | Automatic ingestion from `instructions@collisionengineers.co.uk` | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/outlook-and-background-processing.md#scoped-inbound-outlook-receipt-and-processing) | Required and accepted before V1. |
| INT-03 | Correct handling of staff-forwarded email as real intake | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/outlook-and-background-processing.md#scoped-inbound-outlook-receipt-and-processing) | Required and accepted before V1. |
| INT-08 | Stable source identity, duplicate-delivery handling, and idempotent retry | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-09 | Original inbound source and attachment custody | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Required and accepted before V1. |
| INT-10 | EML and freehand email-body extraction | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Required and accepted before V1. |
| INT-11 | PDF embedded-text and embedded-image extraction | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Required and accepted before V1. |
| INT-12 | DOCX text and every visible image-placement extraction, without deduplicating repeated appearances | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Required and accepted before V1. |
| INT-13 | JPEG and PNG image-led intake | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Required and accepted before V1. |
| INT-17 | Automatic vehicle-registration reading from ordinary vehicle images | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#read-vehicle-registration-from-ordinary-images) | Required and accepted before V1. |
| INT-18 | Bounded, fail-closed processing for unreadable, oversized, or incomplete sources | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-19 | Typed, editable, operator-reviewable extracted case draft | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-20 | Field provenance, validation, missing-value, and contradiction display | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-21 | Human-reviewed extraction cohort, holdout, and field-level accuracy reporting | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/acceptance-and-cutover.md#prove-the-local-workflow-with-genuine-inputs) | Required and accepted before V1. |
| INT-22 | Automatic identification of the correct principal/provider | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-23 | `Needs sorting` queue for uncertain or unsupported intake | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-24 | Manual `Blocked intake` filter with reason, warning, resolve, and retry | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Required and accepted before V1. |
| INT-25 | Automatic case creation from definitive authorised intake | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Required and accepted before V1. |
| INT-26 | Manual case creation through the same business rules | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Required and accepted before V1. |
| INT-27 | Registration-based provisional identity for image-led work | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#establish-provisional-image-identity-before-acceptance) | Required and accepted before V1. |
| INT-29 | Manual linking and reasoned reversal of a mistaken match/merge | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| INT-30 | Preservation of original intake origin after linking or merging | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| MAIL-14 | Detect an exact Outlook Sent item as report-sent evidence | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/outlook-and-background-processing.md#exact-sent-item-report-evidence) | Required and accepted before V1. |
| MAIL-15 | Manually link, unlink, or relink an exact Sent item with a reason | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/outlook-and-background-processing.md#exact-sent-item-report-evidence) | Required and accepted before V1. |

| MAIL-16 | Automatically match the exact report Sent item to its case | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/outlook-and-background-processing.md#exact-sent-item-report-evidence) | Required and accepted before V1. |
| MAIL-18 | Generate copyable chaser messages for staff to send manually | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#surface-due-work-and-manual-chasers) | Required and accepted before V1. |
| TRI-01 | Separate pre-case Triage record and workflow | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-02 | Vehicle-registration gate and `Needs sorting` fallback | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-03 | Open, Awaiting information, Finding recorded, Completed, and Cancelled states | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-04 | Roadworthy/Unroadworthy finding and reasoned replacement | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-05 | Exact reply-chain Sent-item evidence required for completion | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-06 | Reopen and superseding-finding behavior with permanent history | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-07 | Optional later case link, unlink, and relink | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-08 | Dedicated Triage list and detail workspace | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| TRI-09 | Optional Triage assignee, with no due date and no chasers | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/triage-workflow.md#triage-workflow) | Required and accepted before V1. |
| CASE-01 | Every active QDOS case type can travel end to end from intake through accepted case workflow to successful EVA export/handoff | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/acceptance-and-cutover.md#complete-operator-acceptance-and-production-cutover) | Required and accepted before V1. |
| CASE-02 | Inspection cases | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-03 | Standalone Audit cases | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-04 | Inspection + Audit cases and secondary Audit reference | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-07 | Shared principal/year case sequence | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-08 | Repairable `a.` and total-loss `ap.` Audit references | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-09 | Case principal and reference immutability after allocation | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-10 | Wrong-principal `Created in error` closure and linked replacement case | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-identity-and-references.md#allocate-and-represent-active-case-identities) | Required and accepted before V1. |
| CASE-11 | Typed provider, claimant, claim, vehicle, accident, contact, and inspection data | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Required and accepted before V1. |
| CASE-12 | Relationships to staff/Engineer, repairer/bodyshop, insurer, and contacts | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Required and accepted before V1. |
| CASE-13 | Separate staff judgements for instruction completeness and image completeness | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-14 | Configurable completeness gate before Engineer assignment | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-15 | Configurable staff review gate before Engineer assignment | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-16 | `Not ready`, `Review`, and `Held` workflow | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-17 | Due-by date extraction and overdue display | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#surface-due-work-and-manual-chasers) | Required and accepted before V1. |
| CASE-18 | Seven-calendar-day missing-information chase schedule | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#surface-due-work-and-manual-chasers) | Required and accepted before V1. |
| CASE-19 | Hold/release behavior that preserves the chase interval | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#surface-due-work-and-manual-chasers) | Required and accepted before V1. |
| CASE-20 | General case tasks and reminders | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-21 | Successful EVA JSON/image export records the V1 `Sent to Engineer` handoff/proxy; EVA owns the actual named-Engineer assignment | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#export-the-v1-eva-bundle) | Required and accepted before V1. |
| CASE-24 | Post-report completion, provider cancellation, and Collision Engineers rejection outcomes | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-25 | Reasoned reopening into a valid nonterminal state | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-26 | Archive without permanent case deletion | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| CASE-27 | Exclusive edit lease and stale-write protection | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/case-editing-concurrency.md#acquire-renew-and-release-one-case-edit-lease) | Required and accepted before V1. |
| CASE-28 | Roadworthiness and repairable/total-loss findings | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |

| CASE-29 | Inspection address or `Image Based Assessment` | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#resolve-inspection-address-from-reviewed-reference-data) | Required and accepted before V1. |
| CASE-30 | Track the V1 inspection/report stage and EVA handoff without replacing EVA's engineering workflow | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/lifecycle-and-work-management.md#implement-state-reviews-terminal-history-and-matching) | Required and accepted before V1. |
| UI-01 | Operations dashboard/cockpit | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-operational-queues-and-dashboard) | Required and accepted before V1. |
| UI-02 | Case queues for Not ready, Review, and Held | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-operational-queues-and-dashboard) | Required and accepted before V1. |
| UI-03 | V1 intake queues for Needs sorting and Blocked intake | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-operational-queues-and-dashboard) | Required and accepted before V1. |
| UI-04 | In today, Sent to Engineer, and Reports sent day/week activity | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-operational-queues-and-dashboard) | Required and accepted before V1. |
| UI-05 | Click-through filtered work queues | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-operational-queues-and-dashboard) | Required and accepted before V1. |
| UI-06 | Last-updated time and manual refresh | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-operational-queues-and-dashboard) | Required and accepted before V1. |
| UI-07 | Search/filter by reference, registration, claimant, claim, principal, state, Engineer, dates, and origin | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-case-search-and-workspace-actions) | Required and accepted before V1. |
| UI-08 | Three-column intake review workbench | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-case-search-and-workspace-actions) | Required and accepted before V1. |
| UI-09 | Full case workspace | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-case-search-and-workspace-actions) | Required and accepted before V1. |
| UI-11 | Accounts, principals, mailbox allowlist, and configuration workspace | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) | Required and accepted before V1. |
| UI-13 | Accessible keyboard, screen-reader, focus, contrast, and error behavior | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/operator-workspace.md#deliver-case-search-and-workspace-actions) | Required and accepted before V1. |
| DOC-01 | Automatic Box case-folder creation using the Case/PO name | v1 (test folder in box) | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | V1 gate; preserve test-folder scope. |
| DOC-02 | Store source emails, instruction documents, images, correspondence, and reports in Box | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | Required and accepted before V1. |
| DOC-03 | Retained document versions | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | Required and accepted before V1. |
| DOC-04 | Closed-case read-only files and reopen-before-edit behavior | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | Required and accepted before V1. |
| DOC-05 | Logical file removal without destroying history | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | Required and accepted before V1. |
| DOC-06 | Box file-request creation | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | Required and accepted before V1. |
| DOC-07 | Staff upload, view, download, and export actions | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#scoped-box-folder-and-version-custody) | Required and accepted before V1. |
| DOC-08 | Private transient file staging for Worker processing | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Required and accepted before V1. |
| EXT-01 | DVLA/DVSA vehicle lookup | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#enrich-vehicle-data-from-dvla-and-dvsa) | Required and accepted before V1. |
| EXT-02 | MOT history and mileage estimation | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#enrich-vehicle-data-from-dvla-and-dvsa) | Required and accepted before V1. |
| EXT-03 | Operator-approved structured JSON and image-bundle export to EVA | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#export-the-v1-eva-bundle) | Required and accepted before V1. |
| EXT-14 | Manual addition of relevant WhatsApp material | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/box-case-files.md#add-manually-received-whatsapp-material-to-a-case) | Required and accepted before V1. |
| EXT-18 | Inspection-address mapping or prediction | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#resolve-inspection-address-from-reviewed-reference-data) | Required and accepted before V1. |
| MCP-01 | OAuth-authorised internal staff MCP, primarily for Claude Desktop | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/staff-mcp.md#remote-staff-oauth-and-restricted-mcp-tool-surface) | Required and accepted before V1. |
| MCP-02 | MCP case actions through the same Core use cases as the staff app | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/staff-mcp.md#remote-staff-oauth-and-restricted-mcp-tool-surface) | Required and accepted before V1. |
| MCP-03 | MCP intake-queue actions through the same Core use cases as the V1 staff app | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/staff-mcp.md#remote-staff-oauth-and-restricted-mcp-tool-surface) | Required and accepted before V1. |
| MCP-04 | MCP document actions through the same Core use cases as the staff app | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/staff-mcp.md#remote-staff-oauth-and-restricted-mcp-tool-surface) | Required and accepted before V1. |
| OPS-01 | Production staff Web application on Azure | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Required and accepted before V1. |
| OPS-02 | Continuously running Worker for mailbox and background processing | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Required and accepted before V1. |
| OPS-03 | Azure SQL persistence | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Required and accepted before V1. |
| OPS-04 | Safe database migrations and concurrent reference allocation | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#prove-persistence-observability-and-recovery-in-shared-development) | Required and accepted before V1. |
| OPS-05 | Managed identity and least-privilege RBAC between Azure services | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Required and accepted before V1. |

| OPS-06 | Infisical/Key Vault custody for unavoidable third-party secrets | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Required and accepted before V1. |
| OPS-07 | Correlated Web/Worker telemetry and dependency readiness checks | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#prove-persistence-observability-and-recovery-in-shared-development) | Required and accepted before V1. |
| OPS-08 | Alerts for ingestion, processing, Box, matching, chasing, export, security, availability, and cost failures | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#prove-persistence-observability-and-recovery-in-shared-development) | Required and accepted before V1. |
| OPS-09 | Database backup, restore proof, 15-minute RPO, and four-hour RTO | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#prove-persistence-observability-and-recovery-in-shared-development) | Required and accepted before V1. |
| OPS-11 | Production isolated from local development and Azure development/integration resources | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Required and accepted before V1. |
| OPS-13 | Deployment preview, policy/quota checks, health probes, and smoke tests | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#release-immutable-artifacts-safely) | Required and accepted before V1. |
| OPS-14 | Production cutover and previous-artifact rollback procedure | v1 but requires more clarity on details | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#release-immutable-artifacts-safely) | V1 gate; implementation/recovery detail remains open. |
| OPS-20 | Capacity for about eight concurrent staff and 2,000 new cases per month | v1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#prove-persistence-observability-and-recovery-in-shared-development) | Required and accepted before V1. |
| OPS-24 | Direct production deployment from an authorised terminal using committed Bicep through `azd` | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/platform/azure-observability-and-release.md#release-immutable-artifacts-safely) | Required and accepted before V1. |
| DATA-01 | One-time preparation of provider reference data from spreadsheets | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#prepare-reviewed-provider-reference-data) | Required and accepted before V1. |
| DATA-02 | One-time preparation of inspection-address / repairer reference data from spreadsheets | V1 | V1 alpha gate | [Worksheet] | [Primary plan](remainder-delivery/integrations/vehicle-data-and-eva-export.md#prepare-reviewed-inspection-address-reference-data) | Required and accepted before V1. |

## V1.x before V2

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| INT-04 | Activate additional providers during V1.x through the same intake/case workflow using bounded provider reference data and rules | V1.x (before v2) | V1.x before V2 | [Worksheet] | [Primary plan](later-delivery/integrations/additional-provider-activation.md#activate-an-additional-provider) | Additional providers before V2; shared workflow only. |

## Pre-V1 gate

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| OPS-23 | Operator acceptance against the real end-to-end workflow | pre v1 | Pre-V1 gate | [Worksheet] | [Primary plan](remainder-delivery/platform/acceptance-and-cutover.md#complete-operator-acceptance-and-production-cutover) | Required before V1 acceptance. |
| OPS-25 | Collision Engineers management approval before production release | pre V1 | Pre-V1 gate | [Worksheet] | [Primary plan](remainder-delivery/platform/acceptance-and-cutover.md#complete-operator-acceptance-and-production-cutover) | Required before V1 acceptance. |

## V2 beta

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| INT-05 | Automatic ingestion from `desk@collisionengineers.co.uk` | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#ingest-all-four-mailboxes) | Allocation only; owning evidence still required. |
| INT-06 | Automatic ingestion from `engineers@collisionengineers.co.uk` | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#ingest-all-four-mailboxes) | Allocation only; owning evidence still required. |
| INT-07 | Automatic ingestion from `info@collisionengineers.co.uk` | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#ingest-all-four-mailboxes) | Allocation only; owning evidence still required. |
| INT-14 | Automated legacy DOC extraction | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#automate-legacy-doc-and-msg) | Allocation only; owning evidence still required. |
| INT-15 | Automated MSG extraction | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#automate-legacy-doc-and-msg) | Allocation only; owning evidence still required. |
| INT-16 | OCR for scan-like PDF instruction pages | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/source-custody-and-document-processing.md#v2-targeted-scanned-pdf-ocr) | Allocation only; owning evidence still required. |
| INT-28 | Automatic matching of image-led and instruction-led records | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#v2-match-image-led-and-instruction-led-records) | Allocation only; owning evidence still required. |
| MAIL-01 | Identify every inbound mailbox item and its mailbox/thread/message identity | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#classify-and-explain-mail) | Allocation only; owning evidence still required. |
| MAIL-02 | Map detailed email classifications to Receiving work, Query, Other, Needs sorting, or the separate Triage workflow | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#classify-and-explain-mail) | Allocation only; owning evidence still required. |
| MAIL-03 | One shared classification policy across all supported mailboxes | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#classify-and-explain-mail) | Allocation only; owning evidence still required. |
| MAIL-04 | Explainable classification evidence, policy version, and correction history | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#classify-and-explain-mail) | Allocation only; owning evidence still required. |
| MAIL-05 | Recommend the designated Outlook folder for a classified message | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#recommend-confirm-and-move-outlook-items) | Allocation only; owning evidence still required. |
| MAIL-06 | Staff confirmation of a recommended folder move in CollisionSpike | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#recommend-confirm-and-move-outlook-items) | Allocation only; owning evidence still required. |
| MAIL-07 | Move the confirmed message to the designated Outlook folder | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#recommend-confirm-and-move-outlook-items) | Allocation only; owning evidence still required. |
| MAIL-08 | Suggested next actions for classified email | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#suggest-next-actions) | Allocation only; owning evidence still required. |
| MAIL-09 | Automatic association of related email and attachments with a case | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#associate-email-and-cases) | Allocation only; owning evidence still required. |
| MAIL-10 | Manual email/case association, unlink, relink, and correction | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#associate-email-and-cases) | Allocation only; owning evidence still required. |
| MAIL-11 | Browse, search, and view mailbox messages and conversation threads in the app | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#deliver-the-email-workspace) | Allocation only; owning evidence still required. |
| MAIL-13 | Change read state, Outlook categories, flags, or delete messages in the app | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#deliver-the-email-workspace) | Allocation only; owning evidence still required. |
| CASE-23 | Post-report query and dispute work | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/casework/post-report-query-and-dispute.md#resolve-post-report-queries-and-disputes) | Allocation only; owning evidence still required. |
| UI-10 | Full email-management workspace | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#deliver-the-email-workspace) | Allocation only; owning evidence still required. |
| UI-14 | Categorised email queues for Receiving work, Queries, and Other | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#deliver-the-email-workspace) | Allocation only; owning evidence still required. |
| API-01 | Principal-scoped provider submission API | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/provider-submissions.md#receive-principal-scoped-submissions) | Allocation only; owning evidence still required. |
| API-02 | Provider API receipt and processing-status lookup | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/provider-submissions.md#return-provider-receipt-status-and-result) | Allocation only; owning evidence still required. |
| API-03 | Provider API resulting Case/PO lookup | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/provider-submissions.md#return-provider-receipt-status-and-result) | Allocation only; owning evidence still required. |
| API-04 | Provider API credential issue, rotation, and revocation | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/integrations/provider-submissions.md#issue-rotate-and-revoke-provider-credentials) | Allocation only; owning evidence still required. |
| MCP-05 | MCP actions for the broader classified-email workspace | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#expose-classified-email-actions-through-existing-staff-mcp) | Allocation only; owning evidence still required. |
| AI-05 | AI/vision assistance for vehicle images or damage evidence | v2 | V2 beta | [Worksheet] | [Primary plan](remainder-delivery/casework/intake-and-case-acceptance.md#v2-assist-vehicle-image-and-damage-review) | Allocation only; owning evidence still required. |
| MAIL-23 | Map the detailed taxonomy to operational queues and designated Outlook folders | v2 | V2 beta | [Worksheet] | [Primary plan](later-delivery/integrations/email-workspace-and-association.md#map-taxonomy-to-operational-queues-and-folders) | Allocation only; owning evidence still required. |

## V3 release work

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| MAIL-19 | Automatically send chasers or other outbound messages | v3 | V3 release work | [Worksheet] | [Primary plan](later-delivery/integrations/communications-automation.md#automate-chasers) | Allocation only; owning evidence still required. |
| CASE-05 | Diminution cases | v3 | V3 release work | [Worksheet] | [Primary plan](later-delivery/casework/diminution-and-commercial.md#add-diminution-cases) | Allocation only; owning evidence still required. |
| CASE-06 | Commercial cases | v3 | V3 release work | [Worksheet] | [Primary plan](later-delivery/casework/diminution-and-commercial.md#add-commercial-cases) | Allocation only; owning evidence still required. |
| EXT-15 | Automated WhatsApp ingestion and coexistence | V3 | V3 release work | [Worksheet] | [Primary plan](later-delivery/integrations/communications-automation.md#automate-whatsapp-intake-and-coexistence) | Allocation only; owning evidence still required. |
| AI-01 | In-app staff AI assistant | v3 | V3 release work | [Worksheet] | [Primary plan](later-delivery/ai-and-automation/operator-assistance.md#in-app-staff-assistant) | Allocation only; owning evidence still required. |
| AI-02 | AI-assisted email identification/classification | v3 (if rule based insufficient) | V3 release work | [Worksheet] | [Primary plan](later-delivery/ai-and-automation/operator-assistance.md#assist-email-identification-and-actions) | Activate only if rule-based behavior is insufficient and approvals are met. |
| AI-03 | AI-assisted suggested email actions | v3 (if rule based insufficient) | V3 release work | [Worksheet] | [Primary plan](later-delivery/ai-and-automation/operator-assistance.md#assist-email-identification-and-actions) | Activate only if rule-based behavior is insufficient and approvals are met. |
| AI-04 | AI-assisted document extraction and operator review | v3 (if rule based insufficient) | V3 release work | [Worksheet] | [Primary plan](later-delivery/ai-and-automation/operator-assistance.md#assist-document-extraction-and-review) | Activate only if rule-based behavior is insufficient and approvals are met. |
| AI-06 | AI-assisted inspection-address suggestions | v3 *(if rule based insufficient) | V3 release work | [Worksheet] | [Primary plan](later-delivery/ai-and-automation/operator-assistance.md#assist-inspection-address-selection) | Activate only if rule-based behavior is insufficient and approvals are met. |

## V3+ release work

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| MAIL-17 | Automatically send reports | v3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/communications-automation.md#automate-report-sending) | Allocation only; owning evidence still required. |
| CASE-22 | Replace EVA inspection and report-preparation work inside CollisionSpike | v3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#replace-eva-engineering-workflow) | Allocation only; owning evidence still required. |
| EXT-04 | Direct EVA API integration | V3+ (based on eva devs fixing it) | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#activate-direct-eva-api) | Depends on usable EVA vendor capability and separate approval. |
| EXT-05 | Replace EVA Engineer assignment | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#replace-eva-engineering-workflow) | Allocation only; owning evidence still required. |
| EXT-06 | Replace EVA estimating | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#replace-eva-engineering-workflow) | Allocation only; owning evidence still required. |
| EXT-07 | Replace EVA valuation | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#replace-eva-engineering-workflow) | Allocation only; owning evidence still required. |
| EXT-08 | Replace EVA report generation | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#replace-eva-engineering-workflow) | Allocation only; owning evidence still required. |
| EXT-09 | Repair-estimate workflow | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#deliver-estimating-and-valuation-workflows) | Allocation only; owning evidence still required. |
| EXT-10 | Vehicle-valuation workflow | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#deliver-estimating-and-valuation-workflows) | Allocation only; owning evidence still required. |
| EXT-11 | Invoice amount and accounting/invoicing workflow | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/accounting-and-invoicing.md#deliver-accounting-and-invoicing-workflow) | Allocation only; owning evidence still required. |
| EXT-12 | Audatex or another estimating-service integration | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#integrate-approved-estimating-and-valuation-services) | Allocation only; owning evidence still required. |
| EXT-13 | Other valuation-service integrations | V3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#integrate-approved-estimating-and-valuation-services) | Allocation only; owning evidence still required. |
| AI-07 | Staff-selected `AI Assessor` Engineer option in the post-EVA-replacement assignment workflow | v3+ | V3+ release work | [Worksheet] | [Primary plan](later-delivery/integrations/eva-replacement-and-engineering.md#offer-staff-selected-ai-assessor) | Allocation only; owning evidence still required. |

## Never

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| ACC-12 | External/customer application accounts | Never — out of scope and not planned | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#identity-and-external-access-boundaries) | Permanent boundary; not backlog. |
| ACC-13 | Public registration | Never — internal business use only aside from the provider API | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#identity-and-external-access-boundaries) | Permanent boundary; not backlog. |
| ACC-14 | Multi-factor authentication for staff | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#identity-and-external-access-boundaries) | Permanent boundary; not backlog. |
| MAIL-12 | Compose, reply, forward, and send email in the app | Never — automated sending is separate and planned after v3 | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#communications-boundaries) | Permanent boundary; not backlog. |
| UI-12 | Responsive/mobile staff interface | Never — mobile is not planned | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#operator-interface-boundaries) | Permanent boundary; not backlog. |
| DOC-09 | Automated malware scanning of inbound files | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| DOC-10 | Document redaction workflow | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| DOC-11 | Digital signatures | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| DOC-12 | Automated retention and deletion policy | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| DOC-13 | Legal-hold workflow | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| DOC-14 | Subject-access, correction, export, and erasure workflow | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| DOC-15 | Dedicated DPIA/compliance workflow | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#document-governance-boundaries) | Permanent boundary; not backlog. |
| OPS-12 | GitHub Actions deployment using scoped OIDC identities | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| OPS-15 | Separate staging environment | never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| OPS-16 | Deployment slots / Standard S1 hosting | never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| OPS-17 | Private networking | never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| OPS-18 | Zone redundancy | never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| OPS-19 | Multi-region failover | never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| OPS-21 | Quarterly restore/recovery exercise | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#azure-release-and-resilience-boundaries) | Permanent boundary; not backlog. |
| BND-01 | Import predecessor cases or application data | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-02 | Keep the predecessor application available after cutover | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-03 | Reuse predecessor application code | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-04 | SMS integration | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-05 | Microsoft Teams integration | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-06 | Customer/claimant portal | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-07 | Independent Engineer accounts | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-08 | Solicitor, insurer, repairer, or vehicle-owner accounts | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-09 | Separate QA/test environment | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-10 | Separate user-acceptance environment | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |
| BND-11 | Training/demo environment | Never | Never | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#product-and-environment-boundaries) | Permanent boundary; not backlog. |

## Conditional / Unclear

| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| EXT-16 | Collision Engineers guided mobile image capture | Unclear | Conditional / Unclear | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#guided-capture-activation-gates) | Unassigned; later direct decision required. |
| EXT-17 | Tractable or Ravin guided-capture integration | Unclear | Conditional / Unclear | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#guided-capture-activation-gates) | Unassigned; later direct decision required. |
| EXT-19 | Collision Engineers custom application domain | Unclear | Conditional / Unclear | [Worksheet] | [Primary plan](permanent-and-conditional-boundaries.md#custom-domain-activation-gate) | Unassigned; later direct decision required. |

## Non-row direct claim

The worksheet separately states that EVA replacement changes the JSON/API handoff into an Engineer-assignment screen; `AI Assessor` is an explicit staff-selected Engineer option, not an estimating service or automatic route; most Engineer functions follow EVA replacement; estimating-service integration remains separate. AI-07 allocates AI Assessor to V3+.

## Interpretation invariants

- V0 is local pre-alpha. Its real categorisation caller is the ignored-copy EML evaluator, not Outlook or a mock service.
- V1 is a live QDOS alpha gate using staff-forwarded `instructions@` intake and successful EVA JSON/image export as CollisionSpike's `Sent to Engineer` proxy.
- V1.x adds providers through the same provider-neutral workflow before V2.
- V2 owns the provider API, four-mailbox email workspace/management, automatic image/instruction matching, DOC/MSG automation, scan-like PDF OCR, post-report query work, and allocated AI/vision assistance.
- V3/V3+ contains allocated later automation, EVA replacement, Engineer-function, finance, and AI work. Version alone does not activate an external service.
- Principal-code immutability, linked replacement, shared sequences, Audit prefixes, registration-led identity, terminal outcomes, reopening, and seven-day chasers retain their settled meanings.
- Detailed email classification is separate from queues, Triage routing, and Outlook folder destinations.
- The worksheet is retained interview evidence and becomes superseded only for active allocation after exact triple parity is proved.

[Worksheet]: ../../FEATURE_VERSIONING.md
[Q]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md
[Q-users]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#3-users-and-organisations
[Q-life]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#4-the-case-lifecycle
[Q-case]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#5-case-information
[Q-docs]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#6-documents-photographs-and-evidence
[Q-mail]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#7-communications-and-tasks
[Q-integrations]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#8-integrations
[Q-ops]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#13-monitoring-support-and-operations
[Q-scope]: ../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#15-first-release-scope
[RD]: remainder-delivery/README.md
[RD-identity]: remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md
[RD-intake]: remainder-delivery/casework/intake-and-case-acceptance.md
[RD-triage]: remainder-delivery/casework/triage-workflow.md
[RD-provider]: remainder-delivery/integrations/provider-submissions.md
[RD-mcp]: remainder-delivery/integrations/staff-mcp.md
[MAIL]: mailbox-categorisation-and-email-matching/README.md
[DEF]: deferred-capability-architecture/README.md
[UI]: ui-ux/requirements.md
[EVAL]: long-term-local-testing/README.md
