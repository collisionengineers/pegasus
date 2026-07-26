# CollisionSpike feature versioning worksheet

Status: **Completed interview input — retained for exact source evidence**

The active allocation owner is [the canonical feature maturity map](docs/plans/feature-maturity-map.md), and the dependency-ordered route from current proof through V3 and activation-gated V3+ is the [delivery roadmap](docs/plans/delivery-roadmap.md). Exact parity of all 213 unique ID, feature-label, and trimmed-answer triples was proved before this note was added. Those 213 table rows remain the raw direct-decision record; only their active-roadmap role is superseded. This added status note does not claim byte-for-byte preservation of the initially untracked file.

**Original interview instruction (retained):** Fill in the **Your version** column. Use an exact release such as `0.2`, `1.0`, `1.x`, `2.0`, or `3.x+`; use `Later` or `Never` where that is the right answer. Edit incorrect feature wording and add missing capabilities at the end.

**Original interpretation note (retained):** A blank cell was unanswered. Listing something here did not by itself approve or implement it. Anything marked `V1` had to be completed and accepted before the V1 release.

## Users, access, and administration

| ID | Feature | Your version |
|---|---|---|
| ACC-01 | Staff sign-in with CollisionSpike-managed usernames and passwords |V1 |
| ACC-02 | Administrator, Engineer, and User roles | V1|
| ACC-03 | Staff account creation, disabling, access review, and role assignment |V1 |
| ACC-04 | Role-based protection for every non-public page and action |V1 |
| ACC-05 | Principal/provider administration |V1 |
| ACC-06 | Principal-code replacement with linked predecessor and sequence continuity |V1 |
| ACC-07 | Application and workflow configuration managed by Administrators |V1 |
| ACC-08 | Approved Outlook mailbox allowlist managed by Administrators |V1 |
| ACC-09 | Permanent action history for business changes, exports, and material failures |V1 |
| ACC-10 | Separate authentication/security log | V1|
| ACC-11 | Operational telemetry (`content-safe` wording considered unnecessary) | V1 |
| ACC-12 | External/customer application accounts | Never — out of scope and not planned |
| ACC-13 | Public registration | Never — internal business use only aside from the provider API |
| ACC-14 | Multi-factor authentication for staff | Never|



## Intake and source processing

| ID | Feature | Your version |
|---|---|---|
| INT-01 | Manual upload of instruction emails, documents, and vehicle images |v1 |
| INT-02 | Automatic ingestion from `instructions@collisionengineers.co.uk` |v1 |
| INT-03 | Correct handling of staff-forwarded email as real intake | v1|
| INT-04 | Activate additional providers during V1.x through the same intake/case workflow using bounded provider reference data and rules | V1.x (before v2) |
| INT-05 | Automatic ingestion from `desk@collisionengineers.co.uk` |v2 |
| INT-06 | Automatic ingestion from `engineers@collisionengineers.co.uk` |v2 |
| INT-07 | Automatic ingestion from `info@collisionengineers.co.uk` |v2 |
| INT-08 | Stable source identity, duplicate-delivery handling, and idempotent retry |v1 |
| INT-09 | Original inbound source and attachment custody |v1 |
| INT-10 | EML and freehand email-body extraction |v1 |
| INT-11 | PDF embedded-text and embedded-image extraction | v1|
| INT-12 | DOCX text and every visible image-placement extraction, without deduplicating repeated appearances | V1 |
| INT-13 | JPEG and PNG image-led intake | v1|
| INT-14 | Automated legacy DOC extraction |v2 |
| INT-15 | Automated MSG extraction |v2 |
| INT-16 | OCR for scan-like PDF instruction pages |v2 |
| INT-17 | Automatic vehicle-registration reading from ordinary vehicle images |v1 |
| INT-18 | Bounded, fail-closed processing for unreadable, oversized, or incomplete sources | v1|
| INT-19 | Typed, editable, operator-reviewable extracted case draft | v1 |
| INT-20 | Field provenance, validation, missing-value, and contradiction display |v1 |
| INT-21 | Human-reviewed extraction cohort, holdout, and field-level accuracy reporting |v1 |
| INT-22 | Automatic identification of the correct principal/provider |v1 |
| INT-23 | `Needs sorting` queue for uncertain or unsupported intake | v1|
| INT-24 | Manual `Blocked intake` filter with reason, warning, resolve, and retry |v1 |
| INT-25 | Automatic case creation from definitive authorised intake | v1|
| INT-26 | Manual case creation through the same business rules |v1 |
| INT-27 | Registration-based provisional identity for image-led work | v1|
| INT-28 | Automatic matching of image-led and instruction-led records | v2|
| INT-29 | Manual linking and reasoned reversal of a mistaken match/merge |v1 |
| INT-30 | Preservation of original intake origin after linking or merging | v1|

## Email identification and management

| ID | Feature | Your version |
|---|---|---|
| MAIL-01 | Identify every inbound mailbox item and its mailbox/thread/message identity |v2 |
| MAIL-02 | Map detailed email classifications to Receiving work, Query, Other, Needs sorting, or the separate Triage workflow |v2 |
| MAIL-03 | One shared classification policy across all supported mailboxes | v2|
| MAIL-04 | Explainable classification evidence, policy version, and correction history |v2 |
| MAIL-05 | Recommend the designated Outlook folder for a classified message | v2|
| MAIL-06 | Staff confirmation of a recommended folder move in CollisionSpike | v2|
| MAIL-07 | Move the confirmed message to the designated Outlook folder |v2 |
| MAIL-08 | Suggested next actions for classified email |v2 |
| MAIL-09 | Automatic association of related email and attachments with a case |v2 |
| MAIL-10 | Manual email/case association, unlink, relink, and correction |v2 |
| MAIL-11 | Browse, search, and view mailbox messages and conversation threads in the app |v2 |
| MAIL-12 | Compose, reply, forward, and send email in the app | Never — automated sending is separate and planned after v3 |
| MAIL-13 | Change read state, Outlook categories, flags, or delete messages in the app | v2 |
| MAIL-14 | Detect an exact Outlook Sent item as report-sent evidence |v1 |
| MAIL-15 | Manually link, unlink, or relink an exact Sent item with a reason |v1 |
| MAIL-16 | Automatically match the exact report Sent item to its case |v1 |
| MAIL-17 | Automatically send reports | v3+|
| MAIL-18 | Generate copyable chaser messages for staff to send manually | v1|
| MAIL-19 | Automatically send chasers or other outbound messages |v3 |

## Triage

| ID | Feature | Your version |
|---|---|---|
| TRI-01 | Separate pre-case Triage record and workflow | v1|
| TRI-02 | Vehicle-registration gate and `Needs sorting` fallback |v1 |
| TRI-03 | Open, Awaiting information, Finding recorded, Completed, and Cancelled states |v1 |
| TRI-04 | Roadworthy/Unroadworthy finding and reasoned replacement | v1|
| TRI-05 | Exact reply-chain Sent-item evidence required for completion | v1 |
| TRI-06 | Reopen and superseding-finding behavior with permanent history |v1 |
| TRI-07 | Optional later case link, unlink, and relink |v1 |
| TRI-08 | Dedicated Triage list and detail workspace |v1 |
| TRI-09 | Optional Triage assignee, with no due date and no chasers | V1 |

## Cases and workflow

| ID | Feature | Your version |
|---|---|---|
| CASE-01 | Every active QDOS case type can travel end to end from intake through accepted case workflow to successful EVA export/handoff | V1 |
| CASE-02 | Inspection cases |v1 |
| CASE-03 | Standalone Audit cases | v1|
| CASE-04 | Inspection + Audit cases and secondary Audit reference |v1 |
| CASE-05 | Diminution cases | v3|
| CASE-06 | Commercial cases | v3 |
| CASE-07 | Shared principal/year case sequence |v1 |
| CASE-08 | Repairable `a.` and total-loss `ap.` Audit references |v1 |
| CASE-09 | Case principal and reference immutability after allocation | v1|
| CASE-10 | Wrong-principal `Created in error` closure and linked replacement case |v1 |
| CASE-11 | Typed provider, claimant, claim, vehicle, accident, contact, and inspection data |v1 |
| CASE-12 | Relationships to staff/Engineer, repairer/bodyshop, insurer, and contacts | v1|
| CASE-13 | Separate staff judgements for instruction completeness and image completeness | v1|
| CASE-14 | Configurable completeness gate before Engineer assignment |v1 |
| CASE-15 | Configurable staff review gate before Engineer assignment |v1 |
| CASE-16 | `Not ready`, `Review`, and `Held` workflow |v1 |
| CASE-17 | Due-by date extraction and overdue display |v1 |
| CASE-18 | Seven-calendar-day missing-information chase schedule | v1|
| CASE-19 | Hold/release behavior that preserves the chase interval |v1 |
| CASE-20 | General case tasks and reminders |v1 |
| CASE-21 | Successful EVA JSON/image export records the V1 `Sent to Engineer` handoff/proxy; EVA owns the actual named-Engineer assignment | V1 |
| CASE-22 | Replace EVA inspection and report-preparation work inside CollisionSpike | v3+ |
| CASE-23 | Post-report query and dispute work |v2 |
| CASE-24 | Post-report completion, provider cancellation, and Collision Engineers rejection outcomes | V1 |
| CASE-25 | Reasoned reopening into a valid nonterminal state | v1|
| CASE-26 | Archive without permanent case deletion |v1 |
| CASE-27 | Exclusive edit lease and stale-write protection |v1 |
| CASE-28 | Roadworthiness and repairable/total-loss findings |v1 |
| CASE-29 | Inspection address or `Image Based Assessment` | v1|
| CASE-30 | Track the V1 inspection/report stage and EVA handoff without replacing EVA's engineering workflow | V1 |

## Operator interface and finding work

| ID | Feature | Your version |
|---|---|---|
| UI-01 | Operations dashboard/cockpit | v1|
| UI-02 | Case queues for Not ready, Review, and Held | v1|
| UI-03 | V1 intake queues for Needs sorting and Blocked intake | V1 |
| UI-04 | In today, Sent to Engineer, and Reports sent day/week activity |v1 |
| UI-05 | Click-through filtered work queues |v1 |
| UI-06 | Last-updated time and manual refresh |v1 |
| UI-07 | Search/filter by reference, registration, claimant, claim, principal, state, Engineer, dates, and origin | v1|
| UI-08 | Three-column intake review workbench |v1 |
| UI-09 | Full case workspace |v1 |
| UI-10 | Full email-management workspace | v2 |
| UI-11 | Accounts, principals, mailbox allowlist, and configuration workspace |v1 |
| UI-12 | Responsive/mobile staff interface | Never — mobile is not planned |
| UI-13 | Accessible keyboard, screen-reader, focus, contrast, and error behavior |v1 |
| UI-14 | Categorised email queues for Receiving work, Queries, and Other | v2 |

## Documents, files, and Box

| ID | Feature | Your version |
|---|---|---|
| DOC-01 | Automatic Box case-folder creation using the Case/PO name |v1 (test folder in box) |
| DOC-02 | Store source emails, instruction documents, images, correspondence, and reports in Box | v1|
| DOC-03 | Retained document versions |v1 |
| DOC-04 | Closed-case read-only files and reopen-before-edit behavior |v1 |
| DOC-05 | Logical file removal without destroying history |v1 |
| DOC-06 | Box file-request creation |v1 |
| DOC-07 | Staff upload, view, download, and export actions |v1 |
| DOC-08 | Private transient file staging for Worker processing |v1 |
| DOC-09 | Automated malware scanning of inbound files |Never |
| DOC-10 | Document redaction workflow |Never |
| DOC-11 | Digital signatures |Never |
| DOC-12 | Automated retention and deletion policy | Never|
| DOC-13 | Legal-hold workflow |Never |
| DOC-14 | Subject-access, correction, export, and erasure workflow |Never |
| DOC-15 | Dedicated DPIA/compliance workflow |Never |

## External systems and downstream workflows

| ID | Feature | Your version |
|---|---|---|
| EXT-01 | DVLA/DVSA vehicle lookup | v1|
| EXT-02 | MOT history and mileage estimation |v1 |
| EXT-03 | Operator-approved structured JSON and image-bundle export to EVA | v1|
| EXT-04 | Direct EVA API integration | V3+ (based on eva devs fixing it)|
| EXT-05 | Replace EVA Engineer assignment |V3+ |
| EXT-06 | Replace EVA estimating | V3+ |
| EXT-07 | Replace EVA valuation | V3+ |
| EXT-08 | Replace EVA report generation |V3+ |
| EXT-09 | Repair-estimate workflow |V3+ |
| EXT-10 | Vehicle-valuation workflow |V3+ |
| EXT-11 | Invoice amount and accounting/invoicing workflow | V3+|
| EXT-12 | Audatex or another estimating-service integration |V3+ |
| EXT-13 | Other valuation-service integrations |V3+ |
| EXT-14 | Manual addition of relevant WhatsApp material | V1|
| EXT-15 | Automated WhatsApp ingestion and coexistence | V3|
| EXT-16 | Collision Engineers guided mobile image capture | Unclear|
| EXT-17 | Tractable or Ravin guided-capture integration |Unclear |
| EXT-18 | Inspection-address mapping or prediction |V1 |
| EXT-19 | Collision Engineers custom application domain |Unclear |

Additional: When replacing EVA, JSON export/API handoff is replaced by an Engineer-assignment screen. `AI Assessor` is an explicit Engineer option on that screen, not an estimating service and not an automatic route. Most Engineer functions belong after EVA replacement. Integrated estimating services remain a separate future estimating capability.

## API, MCP, AI, and automation

| ID | Feature | Your version |
|---|---|---|
| API-01 | Principal-scoped provider submission API |v2 |
| API-02 | Provider API receipt and processing-status lookup |v2 |
| API-03 | Provider API resulting Case/PO lookup | v2|
| API-04 | Provider API credential issue, rotation, and revocation |v2 |
| MCP-01 | OAuth-authorised internal staff MCP, primarily for Claude Desktop |v1 |
| MCP-02 | MCP case actions through the same Core use cases as the staff app |v1 |
| MCP-03 | MCP intake-queue actions through the same Core use cases as the V1 staff app | V1 |
| MCP-04 | MCP document actions through the same Core use cases as the staff app |v1 |
| MCP-05 | MCP actions for the broader classified-email workspace | v2 |
| AI-01 | In-app staff AI assistant | v3|
| AI-02 | AI-assisted email identification/classification | v3 (if rule based insufficient)|
| AI-03 | AI-assisted suggested email actions | v3 (if rule based insufficient)|
| AI-04 | AI-assisted document extraction and operator review |v3 (if rule based insufficient)|
| AI-05 | AI/vision assistance for vehicle images or damage evidence |v2 |
| AI-06 | AI-assisted inspection-address suggestions |v3 *(if rule based insufficient) |
| AI-07 | Staff-selected `AI Assessor` Engineer option in the post-EVA-replacement assignment workflow | v3+ |

## Hosting, operations, and release readiness

| ID | Capability | Your version |
|---|---|---|
| OPS-01 | Production staff Web application on Azure | v1|
| OPS-02 | Continuously running Worker for mailbox and background processing |v1 |
| OPS-03 | Azure SQL persistence | v1|
| OPS-04 | Safe database migrations and concurrent reference allocation | v1|
| OPS-05 | Managed identity and least-privilege RBAC between Azure services |v1 |
| OPS-06 | Infisical/Key Vault custody for unavoidable third-party secrets |v1 |
| OPS-07 | Correlated Web/Worker telemetry and dependency readiness checks |v1 |
| OPS-08 | Alerts for ingestion, processing, Box, matching, chasing, export, security, availability, and cost failures |v1 |
| OPS-09 | Database backup, restore proof, 15-minute RPO, and four-hour RTO | v1|
| OPS-10 | Azure development/integration environment deployed directly from an authorised terminal | V0 |
| OPS-11 | Production isolated from local development and Azure development/integration resources | V1 |
| OPS-12 | GitHub Actions deployment using scoped OIDC identities | Never |
| OPS-13 | Deployment preview, policy/quota checks, health probes, and smoke tests | v1|
| OPS-14 | Production cutover and previous-artifact rollback procedure | v1 but requires more clarity on details|
| OPS-15 | Separate staging environment |never |
| OPS-16 | Deployment slots / Standard S1 hosting |never |
| OPS-17 | Private networking |never |
| OPS-18 | Zone redundancy | never|
| OPS-19 | Multi-region failover |never |
| OPS-20 | Capacity for about eight concurrent staff and 2,000 new cases per month |v1 |
| OPS-21 | Quarterly restore/recovery exercise | Never |
| OPS-22 | Genuine-corpus local evaluation harness | v0|
| OPS-23 | Operator acceptance against the real end-to-end workflow | pre v1|
| OPS-24 | Direct production deployment from an authorised terminal using committed Bicep through `azd` | V1 |
| OPS-25 | Collision Engineers management approval before production release | pre V1 |

## Explicit product-boundary choices

These are included so that “not planned” is recorded just as clearly as a release assignment.

| ID | Capability or boundary | Your version or `Never` |
|---|---|---|
| BND-01 | Import predecessor cases or application data | Never |
| BND-02 | Keep the predecessor application available after cutover | Never |
| BND-03 | Reuse predecessor application code | Never |
| BND-04 | SMS integration | Never |
| BND-05 | Microsoft Teams integration | Never |
| BND-06 | Customer/claimant portal | Never |
| BND-07 | Independent Engineer accounts | Never |
| BND-08 | Solicitor, insurer, repairer, or vehicle-owner accounts | Never |
| BND-09 | Separate QA/test environment | Never |
| BND-10 | Separate user-acceptance environment | Never |
| BND-11 | Training/demo environment | Never |

## Missing features or corrections

Add rows here if anything above is absent, combined incorrectly, or worded wrongly.

| ID | Feature or correction | Your version |
|---|---|---|
| DATA-01 | One-time preparation of provider reference data from spreadsheets | V1 |
| DATA-02 | One-time preparation of inspection-address / repairer reference data from spreadsheets | V1 |
| EVAL-01 | Local development-only EML categorisation evaluator using `unchecked` and `checked` workspace folders | V0 |
| EVAL-02 | Reviewer selects from the detailed Received/Sent/Reply taxonomy and records required reasoning | V0 |
| EVAL-03 | `Other` category lets the reviewer enter a new category name and reasoning | V0 |
| EVAL-04 | Moving the reviewed workspace EML into `checked` records the human result | V0 |
| EVAL-05 | Display the rule-generated category and evidence beside the human review once rules exist | V0 |
| MAIL-20 | Run live provider-specific instruction-email categorisation against `.eml` files in the local folder-based evaluator | V0 |
| MAIL-21 | Minimum shared Core classification foundation: versioned rules, decision evidence, ambiguity outcome, and acceptance cohort | V0 |
| MAIL-22 | Detailed email taxonomy from `docs/reference/CollisionSPikeCurrenttree.txt`, including Received/Sent categories, subtypes, and mirrored Reply classifications | V0 |
| MAIL-23 | Map the detailed taxonomy to operational queues and designated Outlook folders | v2 |

The evaluator operates on a dedicated ignored working copy. It must never move, rename, or modify the repository's immutable `corpus/` originals.
