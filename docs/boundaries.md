# Boundaries — deferred capabilities and excluded implementations

This document owns what Pegasus deliberately does **not** implement yet, and the
seams preserved so a capability can be added later without dormant behaviour.
These are normative boundary rules, not scheduling data. Permanent product
"Not planned" boundaries are owned by the [PRD](prd/README.md); the schedule and
capability-ID registry by [capabilities](capabilities.md); required behaviour by
the [FRDs](frd/README.md); technical decisions by the [ADRs](adr/README.md).

## Deferred capabilities and preserved seams

Deferred capabilities remain named in [capabilities](capabilities.md). Preserving a seam means retaining the stable identity/data/port needed to add the capability later without implementing dormant behavior.

| Deferred area | Preserved seam or data identity | Excluded until activation | Activation evidence |
| --- | --- | --- | --- |
| additional mailboxes and classification | mailbox/source/message/occurrence identity; provider/domain route identity | live Graph caller, automated application of the settled taxonomy beyond the delivered recorded-only QDOS-route classification (MAIL-21/22), mailbox mutation | accepted rule predicates and holdout, exact mailbox/folder scopes, test mailbox, Worker caller, recovery, and operator acceptance |
| scanned-document OCR | source hash, scan-like decision, page/image provenance | OCR service, flag, route, fallback | accepted OCR slice, provider/licensing/security decision, genuine cohort evaluation, caller and recovery proof |
| provider APIs | intake command, source/correlation/idempotency identity | endpoint, credentials, retry client, activation | provider contract, credential/scopes, failure/recovery, real caller and acceptance |
| EVA optional handoff | Pegasus owns engineering and final reports in v1; existing EVA submission identity/outcomes remain separate | unapproved vendor operations and assumptions of external delivery | exact vendor contract, credentials, safe unknown handling, caller and operator acceptance |
| EVA API updates | submission identity, EVA claim identifiers | updating an EVA claim over the API | a suitable update contract; EVA's current update endpoints do not fit the use case (2026-08-27), so a submitted case is never updated |
| guided capture and vehicle data | request/source/vehicle fact provenance | live vendor route, OCR lookup, auto-acceptance | vendor contract, confidence/human confirmation rule, data-age/source policy, failure/recovery and evaluation |
| automated correspondence/chasing | action, channel, party, draft and delivery-evidence identities; staff-initiated Reply/Forward/Compose from an approved mailbox is in scope under [ADR-0036](adr/0036-outbound-mail-via-approved-mailbox.md) | autonomous or automated sending, template campaigns, autonomous completion, sending from any non-approved identity | allocation, approved channel policy, exact send scopes, pre-send approval and delivery proof |
| AI assistance | typed evidence/proposal/review identity; the shared AI job ledger (in scope under ADR-0035, AUTO-009: Estimate, Unidentified resolution, Query response and Unidentified-queue pass jobs, `AI-10`) | direct mutation, approval, business policy | accepted Core proposal port, representative evaluation, abstention/challenge gates, human approval, caller proof, and capability-specific capacity measurement |
| Diminution, Commercial, post-report dispute and finance | stable case/work/document/action identities | dormant case types, calculations, invoicing/accounting routes | allocated release, accepted Core contract, source/provider decisions, UI/caller and operator acceptance |
| production deployment and migration | versioned schema/release/evidence identities | provisioning, deployment, predecessor deletion or data migration | exact target approval, validated IaC, migration/rollback plan, deployed caller proof and acceptance |
| direct estimating-service access | v1 includes per-Engineer Glass's repair estimates and canonical Glass's/Audatex file import | Glass's valuation service and unapproved Audatex direct launch | operator-owned live Glass's acceptance; source-labelled import and credential/recovery proof |
| standalone Images list | the Vehicle image detail page as the image record (D1) | a separate Images list page — absent, not disabled (D21, 2026-09-01) | an accepted operator need and a completed design route |
| runtime-managed email and document templates | the governed renderer template and stylesheet assets embedded by Infrastructure | any runtime template editor or caller-selectable template — absent, not disabled (D21, 2026-09-01) | an accepted template-authority, versioning and approval contract |
| 2,000-case capacity tier | the tier-10 cohort and soak evidence shape in [engineering](engineering.md#required-evidence-tiers) | the cohort/soak run itself — **not run by this programme and never represented as passing** (D27, 2026-09-01); per-ticket concurrency tests still run | the separate evidence spike `PLAT-066`, which sits outside EPIC-011 |
| AutoTrader research inside Pegasus | the `MarketResearch` AI job, its findings document retained as Case evidence and the `AI market research` valuation entry ([FRD-11 § AI Job List](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list)) | scraping or an AutoTrader integration inside Pegasus — **absent**; the operator's external connector performs the research and completes the job through the Automation Actor (D35, 2026-09-02) | none in this programme; an in-Pegasus market-research caller would need its own accepted vendor contract, evaluation and caller proof |
| Case record layout switch | one scrolling Case record with sticky ribbon, action bar and section jump-nav ([FRD-12 § Case workspace](frd/frd-12-operator-experience.md#case-workspace)) | a Scroll/Tabs switch — **absent** (D29, 2026-09-02); sections as tabs remain the rule for records other than the Case record | none; the single-scroll record is the decision |
| `EXT-10` valuation adjustments | the valuation entry with source, date, time, mileage, retail and trade values, plus guide month (`CASE-029`); the Glass's valuation and Glass's estimate-import label entries kept separate (D40, 2026-09-02) | CAP HPI, AutoTrader as a manual source, Vehicle data, valuation adjustments, rationale and revaluation history | `EXT-10`'s later allocation (`TICK-083`) and, for adapters, `EXT-13` |

No irreversible choice is made merely to reserve a seam. New top-level projects, stores, runtimes, migration streams, or deployment units require an accepted ADR proving the existing boundary cannot carry the work.

The 6 September 2026 operator decision also defers the one-off customer
workflow and additional spreadsheet-driven recipient/package/chase and
garage-procedure automation. Named location defaults, address suggestions and
top-15 extraction remain included. Staff initiate all report/chaser sends.
