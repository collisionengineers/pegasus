# Change: Orient the repository around Pegasus

```yaml
id: 2026-07-27-pegasus-repository-orientation
type: decision
status: in_progress
risk: high
created: 2026-07-27
updated: 2026-07-27
issue: https://github.com/collisionengineers/pegasus/issues/6
pull_request: pending
baseline: d0965e19230d971166a5ae8fc8b894c6053849e2
target_release: 0.1.0-alpha.1
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
```

## Summary

Adopt **Pegasus** as the active product and repository identity, distil the
management-supplied Pegasus system plan into canonical product owners, retain
EVA only as the current manual Engineer handoff until its functions are
replaced independently, add non-deployed source workspaces, and convert active
release language to Semantic Versioning and allocation horizons. This record is
change evidence and the atomic source crosswalk; it is not a second requirement
database.

## Source proof and retirement rule

Management supplied `PegasusSystemPlan.md` and `PegasusSystemPlan.docx` on
2026-07-27 as draft evidence dated 2026-07-26. The observed source identities
are:

- Markdown SHA-256: `4eb3a75ef7d9066184be94bca5612653e78f49b8f3d3c661dee6da08f1e6655b`;
- DOCX SHA-256: `c86ea81d04314c1a88200b59bcf89037d6548c516af91779fe2d2bf068e21e29`;
- normalized Markdown and DOCX: **2,510 whitespace tokens each, zero token differences**.

The parity check strips Markdown headings, emphasis, list/table syntax and
escape characters, skips table-separator rows, collapses whitespace, and then
compares the resulting token sequence to the DOCX document-text sequence. The
atomic crosswalk below gives every non-empty source paragraph, bullet and table
row at least one non-empty durable disposition. Once the canonical owners and
capability inventory validate, both drafts and their empty source directory are
deleted. Their hashes, parity result, provenance and this crosswalk are retained;
no transcript, archive copy, README or second requirements ledger is created.

## Authorities and current evidence

- Canonical product requirements remain under `docs/product/`; stable allocation
  remains solely in `docs/product/capabilities.md` and `docs/roadmap.md`.
- `Pegasus.Core` remains the single owner of business policy and accepted case
  truth. Infrastructure adapts external systems; Web and Worker remain
  composition roots.
- The current runtime has four production projects. Development-only manual
  intake is the only mutating caller. Provider reference data is implemented and
  queried in integration tests; no Web or Worker caller is claimed.
- EVA retains Engineer assignment, estimating, valuation and report preparation.
  The current Pegasus release produces only the operator-approved manual
  13-key JSON/image handoff; no EVA network call is made.
- Imported renderer, document-extraction, AI Centre and Agent Skills source is
  implementation evidence only. It is not a Pegasus caller, deployment or
  accepted product capability.
- Intended, implemented, deployed and accepted are separate states. No source
  row below claims deployment.

## Resolved document contradictions

| Decision | Resolution |
| --- | --- |
| `DOC-CON-001` | Active product, source, configuration and target-infrastructure identities become Pegasus. Historical/supplied evidence, persisted EF migration identities and exact legacy external resource identities remain factual exceptions. |
| `DOC-CON-002` | The GitHub repository becomes `collisionengineers/pegasus`, the local checkout becomes `pegasus`, and GitHub Project 3 becomes **Pegasus Delivery**. |
| `DOC-CON-003` | Document extraction, deterministic report rendering, Collision AI Centre and Agent Skills are imported as durable, independently buildable source workspaces. They gain no production reference or caller in this change. |
| `DOC-CON-004` | Collision AI Centre owns future agent harnesses, model selection, separately governed fine-tuning, retrieval and skills. `Pegasus.Core` remains the only business-policy and accepted-case-truth owner; AI output is always a proposal. |
| `DOC-CON-005` | The source-specific “Claude button” becomes deferred vendor-neutral **Send to AI**. No Anthropic or other direct model API is in scope, and no Claude Code, Cowork or Desktop job-queue support is claimed until separately proved. |
| `DOC-CON-006` | `0.1.0-alpha.1` gains `INT-31`: staff may create temporary, revocable, request-scoped upload links. The link creates no external account and exposes no case or request state. |
| `DOC-CON-007` | General authenticated staff compose/reply/forward/send (`MAIL-12`) moves from `Not planned` to `Later`/`unallocated`; the separately gated targeted report-send transaction remains `MAIL-17`. |
| `DOC-CON-008` | Administrator is the superuser role. Andrew and Alex are initial assignments held as application data/configuration; no person, email address or bypass is compiled into authorization. |
| `DOC-CON-009` | The current release uses deterministic, operator-approved EVA drag-and-drop JSON, selected custody-confirmed images and a hash manifest. EVA retains engineering authority until each replacement slice is separately contracted, caller-proved and accepted. |
| `DOC-CON-010` | EVA screenshots are data/decision evidence, not navigation authority. Pegasus uses one case-centred Engineer workbench with progressive sections and no duplicate domain ownership. |

## Scope

### Included

- Product capability and area updates for the atomic source claims below,
  including valuation, Engineer information, reports, targeted sending,
  correspondence, accounts/invoicing, management information and AI proposals.
- Consolidation of the exact EVA drag-and-drop examples and 12 Engineer screens.
- Clean active identity rename, SemVer/horizon conversion and retirement of loose
  root planning documents into history.
- Four independently buildable, non-caller workspaces and independent CI health
  checks.
- Target IaC rename only; no Azure read, write, deployment or resource mutation.

### Excluded

- Runtime implementation of `INT-31`, automatic pairing, EVA replacement,
  report rendering, valuation adapters, general/targeted email sending,
  management dashboards, AI work queues, model calls, desktop clients or external
  portals.
- Any inference that source incorporation, project registration, a build or a
  structural check proves a Pegasus production caller.
- Live external-service acceptance, provider contract acceptance, Azure changes,
  migration of ignored Development state, or repository merge.

## Deferred-capability impact

The preserved seams are the immutable case/revision identity, typed evidence
manifest, Core-owned repair specification and valuation decisions, deterministic
render contract, and AI work-request/proposal/review contract. Deferred
capabilities remain `unallocated` unless their existing target says otherwise.
This change makes no irreversible vendor transport choice and creates no dormant
runtime integration. Activation requires a capability-specific change record,
actual caller, recovery proof and acceptance evidence.

## Atomic source crosswalk

`S<section>-L<line>` locators refer to the deleted Markdown source as shown in
this pull request's deletion diff. A suffix splits one source line only where
its clauses have different durable owners. Table headings and source metadata
are retained as provenance rows; they are not product requirements.

| Source | Normalized atomic claim | Durable owner / capability | Allocation | Evidence state |
| --- | --- | --- | --- | --- |
| `Stable-L0` | The crosswalk structure is locator, claim, durable owner, exact allocation, and evidence state. | `this change record` | provenance | `accepted` |
| `Smetadata-L1` | The source document title is “PEGASUS”. | `this change record` | provenance | `accepted` |
| `Smetadata-L3` | The source is a system plan for Collision Engineers Ltd. | `this change record` | provenance | `accepted` |
| `Smetadata-L5` | The plan was prepared for Andrew in the owner role, Alex in the developer role, and the CE team. | `this change record` | provenance | `accepted` |
| `Smetadata-L7` | The source is dated 26 July 2026 and has status “Draft for discussion”. | `this change record` | provenance | `accepted` |
| `S1-L11a` | Pegasus is Collision Engineers’ case-management and reporting product and is intended to become the place where jobs are created, assessed, reported, and tracked. | `docs/product/index.md` | accepted authority | `accepted` |
| `S1-L11b` | Keep EVA and its surrounding spreadsheets/manual steps in parallel until the deferred replacement slice, then replace that workflow with Pegasus. | `EXT-03` | Now/0.1.0-alpha.1 | `intended` |
| `S1-L11c` | Box remains the backing file store for Pegasus-managed case files. | `DOC-01` | Now/0.1.0-alpha.1 | `accepted` |
| `S1-L13` | Capture each job once as structured canonical data and render the assessment report, fee note, audit report, diminution report, addendum, query response, invoice, and management statistic from that source so outputs require no retyping and cannot disagree. | `CASE-31` | Later/unallocated | `intended` |
| `S1-L15a` | Assign post-EVA AI Assessor work within the case workflow to read relevant instruction context and draft image-based assessments, with an engineer reviewing and approving every outward result. | `AI-07` | Later/unallocated | `intended` |
| `S1-L15b` | Generate query-response proposals for engineer review and approval rather than allowing autonomous outward responses. | `AI-08` | Later/unallocated | `intended` |
| `S1-L15c` | An existing deterministic report-renderer source implements computed-once figures and stable output rendering, but it has no production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S2-L19` | The planning baseline is 1,000–1,200 jobs per month, at which volume the current workflow exhibits the three documented problem classes. | `docs/product/areas/platform-and-operator-experience.md` | accepted authority | `accepted` |
| `S2-L21` | The current EVA-era manual bundle consumes two to three administrators on spreadsheet logging, missing-half chasing, WhatsApp downloads, EVA uploads, manual Box-folder creation, and reference-number selection, with attendant error risk. | `EXT-03` | Now/0.1.0-alpha.1 | `accepted` |
| `S2-L23` | The current EVA-era completion workflow makes engineers export PDFs, file them in Box, find the original instruction email, send and delete messages, and mark jobs complete after their expert work is finished. | `EXT-03` | Now/0.1.0-alpha.1 | `accepted` |
| `S2-L25` | EVA vendor dependence currently delays or prevents small desired changes, including a contract-repair percentage slider, one-click diminution reporting, and a vendor-neutral AI assessment action. | `EXT-03` | Now/0.1.0-alpha.1 | `accepted` |
| `S2-L27` | Pegasus is intended to turn product changes into owner-controlled work and shift administration from routine entry to exception monitoring. | `docs/product/index.md` | accepted authority | `accepted` |
| `S3-L31a` | Orchestrate instruction intake from the provider-API or single-inbox channels by reading and extracting details, allocating the next reference, creating the job, requesting its Box folder, and placing the job in the holding pen. | `CASE-31` | Later/unallocated | `intended` |
| `S3-L31b` | Automatically pair each arriving image set with its instruction-backed job regardless of whether images arrive through a request-scoped upload link or the occasional manual WhatsApp fallback. | `INT-28` | Next/unallocated | `intended` |
| `S3-L31c` | Notify the team as soon as both job halves are present and mark the job complete and ready to proceed. | `INT-32` | Next/unallocated | `intended` |
| `S3-L33a` | Obtain valuation evidence from CAP, Glass’s, and Cazana, retain mileage and engineer notes with it, and expose it for readiness decisions. | `EXT-10` | Later/unallocated | `intended` |
| `S3-L33b` | Arrange the engineer workbench so a ready job can move through a repair-specification route, expert decisions covering value, deductions, outcome, category, salvage, and roadworthiness, and report generation. | `UI-15` | Later/unallocated | `intended` |
| `S3-L33c` | Provide one target-report action that replies on the original instruction thread, applies the required recipients, triggers filing, completes the job, and records the management event. | `MAIL-17` | Later/unallocated | `intended` |
| `S3-L35a` | Use the later case query and held job data to produce a CE-house-style response proposal for engineer approval. | `AI-08` | Later/unallocated | `intended` |
| `S3-L35b` | Generate any required diminution report from the data already held for the job. | `RPT-04` | Later/unallocated | `intended` |
| `S3-L35c` | Generate any required addendum from the data already held for the job. | `RPT-05` | Later/unallocated | `intended` |
| `S4.1-L41a` | Treat an instruction and an image set as order-independent job halves, place each arrival in a holding pen, and pair the halves automatically when both are available. | `INT-28` | Next/unallocated | `intended` |
| `S4.1-L41b` | Keep every unpaired item visible with its age and chase status, and notify the team when the second half makes the job complete. | `INT-32` | Next/unallocated | `intended` |
| `S4.1-L43` | Constrain supported intake to the three declared channels rather than allowing an open-ended channel set. | `CASE-31` | Later/unallocated | `intended` |
| `S4.1-L45` | Accept structured instructions from larger work providers through a provider API without email-reading steps. | `API-01` | Next/unallocated | `intended` |
| `S4.1-L47` | Use one instruction inbox for non-API work; log each email, read instructions using the maintained PDF mappings, and populate client details, accident date, and principal from the extracted data. | `CASE-31` | Later/unallocated | `intended` |
| `S4.1-L49a` | Give clients, bodyshops, and storage yards a request-scoped web link through which they can upload images. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L49b` | Do not create a persistent external client, bodyshop, or storage-yard portal; the upload-link capability is the scoped exception. | `BND-06` | Not planned/unallocated | `accepted` |
| `S4.1-L49c` | Retain WhatsApp only as an occasional manual fallback in which staff drag received images into the scoped upload flow. | `EXT-14` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L51a` | Allow staff to send a request-scoped upload link to a client. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L51b` | Allow staff to log a bodyshop chase and see which half each incomplete job is awaiting. | `INT-32` | Next/unallocated | `intended` |
| `S4.2-L55a` | Automate unique reference allocation, job creation, mapped-instruction population, and the final readiness transition while allowing administrators to add mileage and engineer notes such as unroadworthiness or a customer preference for total loss. | `CASE-31` | Later/unallocated | `intended` |
| `S4.2-L55b` | Create the case Box folder and file the instruction, images, and notes into it as part of automated setup. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55c` | Provide external adapters for CAP, Glass’s, and Cazana guide-value retrieval and DVLA registration lookup, including make, model, year, engine, and fuel, with optional prefetch before a user opens the job. | `EXT-13` | Later/unallocated | `intended` |
| `S4.2-L57` | Design queue operations so one team member can monitor and resolve unmatched images, unmapped instructions, and unusual cases instead of requiring two to three people for data entry. | `docs/product/areas/platform-and-operator-experience.md` | accepted authority | `intended` |
| `S4.3-L61` | Arrange the engineer workspace around expert decisions and expose exactly the three declared repair-specification entry routes without unrelated administration. | `UI-15` | Later/unallocated | `intended` |
| `S4.3-L63a` | Support Glass’s as a repair-specification route equivalent to the traditional EVA-integrated route. | `ENG-01` | Later/unallocated | `intended` |
| `S4.3-L63b` | Glass’s integration is an external dependency that must remain explicit until its access and wording are resolved. | `docs/product/open-decisions.md` | unallocated blocker | `accepted` |
| `S4.3-L65` | Support an Audatex route in which the estimate is built in Audatex, printed to PDF, attached to the job, and mapped into Pegasus’s standard repair-specification format. | `EXT-12` | Later/unallocated | `intended` |
| `S4.3-L67a` | Offer a vendor-neutral “Send to AI” repair-specification route that proposes an image-based specification for engineer review and approval, including clear-total-loss cases. | `ENG-01` | Later/unallocated | `intended` |
| `S4.3-L67b` | Carry the AI repair-specification work through a durable vendor-neutral request, lease, proposal, and review contract rather than a direct model API. | `AI-09` | Later/unallocated | `intended` |
| `S4.3-L67c` | An existing AI/skills source implements relevant clear-total-loss assessment behavior, but it has no production caller. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S4.3-L69a` | Allow the engineer to set a contract-repair cap, for example 80% of pre-accident value, and obtain a proposed assessment already targeted to that cap without repeated manual adjustment. | `ENG-01` | Later/unallocated | `intended` |
| `S4.3-L69b` | Submit and review the capped assessment through the vendor-neutral “Send to AI” transport contract. | `AI-09` | Later/unallocated | `intended` |
| `S4.3-L71a` | Require the engineer to complete final value and deductions, outcome from total loss/repairable/cash in lieu/contract repair, salvage category and value, and roadworthiness with reason. | `ENG-02` | Later/unallocated | `intended` |
| `S4.3-L71b` | Compose or compute every derivable report value and narrative, including settlement figures, rather than asking the engineer to retype them. | `RPT-01` | Later/unallocated | `intended` |
| `S4.4-L75a` | Produce the core assessment through a deterministic engine with computed-once figures, validation before render, fixed CE layout, four outcome variants, and an included fee-note page. | `RPT-01` | Later/unallocated | `intended` |
| `S4.4-L75b` | The existing report-renderer source embodies the July 2026 deterministic-generation design but has no production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S4.4-L75c` | The fixed CE visual and house-style assets are governed by the design authority. | `design/` | accepted authority | `accepted` |
| `S4.4-L77a` | Render a conservative report and a maximised audit report from one job containing two repair specifications. | `RPT-03` | Later/unallocated | `intended` |
| `S4.4-L77b` | Record the uplift between the conservative and maximised audit specifications as an operational management statistic. | `MI-03` | Later/unallocated | `intended` |
| `S4.4-L79` | Generate a diminution-in-value report from the original job data after the engineer supplies the percentage. | `RPT-04` | Later/unallocated | `intended` |
| `S4.4-L81` | Generate an addendum from existing job data with the amendment applied and without retyping the job. | `RPT-05` | Later/unallocated | `intended` |
| `S4.4-L83a` | Generate the fee note and itemised repair-specification breakdown as report-engine outputs. | `RPT-01` | Later/unallocated | `intended` |
| `S4.4-L83b` | File the fee note and itemised repair-specification breakdown in Box alongside the report. | `DOC-04` | Now/0.1.0-alpha.1 | `intended` |
| `S4.5-L87a` | Send the report and fee note from Pegasus on the original instruction email thread or API route, using each principal’s saved CC suggestions, delivery preferences such as separate report and image attachments, and standing notes; then mark the job complete. | `MAIL-17` | Later/unallocated | `intended` |
| `S4.5-L87b` | File the sent report-package items into Box. | `DOC-05` | Now/0.1.0-alpha.1 | `intended` |
| `S4.5-L87c` | Stamp the operational management-information event when the report package is sent and the job completes. | `MI-03` | Later/unallocated | `intended` |
| `S4.6-L91a` | Keep the case alive after reporting and display its complete email chain and correspondence history on the job. | `MAIL-11` | Next/unallocated | `intended` |
| `S4.6-L91b` | For a defendant-engineer challenge, dispute, requested adjustment, or similar query, propose a job-aware CE-house-style letterhead response for the engineer to accept or amend before sending. | `AI-08` | Later/unallocated | `intended` |
| `S4.6-L91c` | The existing AI/skills source contains the earlier cost-defence rebuttal format and house-style rules, but it has no production caller. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S4.6-L93` | Tag every query with a maintained taxonomy, including supplementary request, repair-cost challenge, and valuation dispute, so query-type training statistics can be produced. | `CASE-23` | Next/unallocated | `intended` |
| `S4.7-L97` | Pegasus should track every practicable event so management statistics are produced as a by-product of normal case activity rather than through separate manual work. | `MI-03` | Later/unallocated | `intended` |
| `S4.7-L99` | Per-engineer MI should show reports per day, jobs completed, query rate, query types and audit-report uplift, and support coaching using patterns such as supplementaries from missed hidden damage or excessive repair-cost challenges. | `MI-01` | Later/unallocated | `intended` |
| `S4.7-L101` | Per-principal MI should show report counts by type and period and feed those figures directly into invoice generation. | `MI-02` | Later/unallocated | `intended` |
| `S4.7-L103` | Operational MI should show holding-pen age, instruction-to-images time, ready-to-sent time and overall turnaround. | `MI-03` | Later/unallocated | `intended` |
| `S4.7-L105a` | Every engineer and administration team member should have an individual login. | `ACC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S4.7-L105b` | Accounts information and management statistics should be restricted to the superuser role. | `ACC-03` | Now/0.1.0-alpha.1 | `intended` |
| `S4.7-L105c` | Andrew is the initial superuser/Administrator assignment. | `ACC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.8-L109a` | Box should remain the archive and file store behind Pegasus. | `DOC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S4.8-L109b` | Pegasus should create each case folder through the Box API. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.8-L109c` | Pegasus should automatically file the instruction, images, notes, reports, fee notes, breakdowns and sent correspondence so staff do not handle Box manually and it serves as the audit-proof case library. | `DOC-03` | Now/0.1.0-alpha.1 | `intended` |
| `S5-L113` | The change record introduces the following items as substantial existing Pegasus assets, without establishing production deployment. | `this change record provenance` | provenance | `accepted` |
| `S5-L115` | Structural table header: Asset \| What it gives Pegasus. | `this change record provenance` | provenance | `accepted` |
| `S5-L117a` | The imported report generator contains deterministic PDF rendering, computed-once figures, validation, four outcome variants, a fee-note page and a variables walkthrough mapping fields to dashboard input types; it has no established production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S5-L117b` | The source records the report generator status as locked in July 2026. | `this change record provenance` | provenance | `accepted` |
| `S5-L118` | The imported renderer uses a structured JSON job schema that is the natural single-source core data model for Pegasus, but it has no established production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S5-L119a` | An imported image-to-structured-assessment workflow exists and works for clear total-loss examples, but no production caller or direct model API is established. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S5-L119b` | The clear-total-loss structured assessment outcome should become one of the Engineer repair-specification routes exposed as vendor-neutral Send to AI. | `ENG-01` | Later/unallocated | `intended` |
| `S5-L119c` | Send to AI should use the durable vendor-neutral request, lease, proposal and review transport contract. | `AI-09` | Later/unallocated | `intended` |
| `S5-L120a` | An imported cost-defence skill can produce court-addressed cost-justification documents in a fixed house style, but it has no established production caller. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S5-L120b` | The query-response proposal workflow should use the cost-defence capability as the basis for formal query responses. | `AI-08` | Later/unallocated | `intended` |
| `S5-L121` | The codified CE house-style authority defines tone, wording and banned terms for outbound letters, emails and rebuttals drafted with AI assistance. | `design/` | accepted authority | `accepted` |
| `S5-L122` | The existing CE design assets provide brand tokens, fonts, letterhead and document layout for Pegasus screens and outputs. | `design/` | accepted authority | `implemented` |
| `S6-L126` | External-dependency conversations should begin immediately because their answers block or shape build decisions. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L128` | Structural table header: Dependency \| What we need to find out. | `this change record provenance` | provenance | `accepted` |
| `S6-L130a` | The team must resolve whether Glass’s repair estimating can be integrated directly outside EVA, including licensing, API or embedded access and cost. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L130b` | A direct Glass’s estimating replacement should be provided only if access and commercial terms are viable. | `EXT-06` | Later/unallocated | `intended` |
| `S6-L130c` | If Glass’s is unavailable, Engineers should retain the Audatex-import and Send-to-AI repair-specification routes. | `ENG-01` | Later/unallocated | `intended` |
| `S6-L131a` | API access, licensing and terms for CAP, Glass’s and Cazana outside EVA must be confirmed. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L131b` | Pegasus should replace EVA-mediated valuation access with supported direct valuation integrations where terms permit. | `EXT-07` | Later/unallocated | `intended` |
| `S6-L132` | The provider API capability must establish which larger work providers can submit instructions by API and which formats they use. | `API-01` | Next/unallocated | `intended` |
| `S6-L133a` | Box API access should support case-folder creation and automated filing. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S6-L133b` | The source treats the Box API as mature and the proposed integration as straightforward. | `DOC-02` | Now/0.1.0-alpha.1 | `accepted` |
| `S6-L134` | External adapters should provide DVLA vehicle-detail lookup and a provider lookup such as Experian AutoCheck for the mandatory vehicle-history check. | `EXT-13` | Later/unallocated | `intended` |
| `S6-L135` | The Audatex integration must confirm that its PDF mapping covers the variants Engineers produce so drag-in import is reliable. | `EXT-12` | Later/unallocated | `intended` |
| `S7-L139a` | Each build phase should be independently useful while EVA remains available in parallel. | `EXT-03` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L139b` | After Pegasus covers the full flow, work should migrate from EVA provider by provider through a deferred replacement slice. | `CASE-22` | Later/unallocated | `intended` |
| `S7-L139c` | The source assigns Alex as build lead and records AI-assisted development throughout the build. | `this change record provenance` | provenance | `accepted` |
| `S7-L141` | Structural table header: Phase \| Build \| Why this order. | `this change record provenance` | provenance | `accepted` |
| `S7-L143a` | Phase 0 should run dependency enquiries before build decisions, especially for Glass’s and valuation APIs. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S7-L143b` | Phase 0 should confirm the structured job data model, using the substantially completed report specification as its basis. | `CASE-31` | Later/unallocated | `intended` |
| `S7-L144a` | Phase 1 should provide a request-scoped image-upload portal that can operate while cases continue through EVA. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L144b` | Phase 1 should automate the single intake inbox and pair received instructions and images into jobs. | `INT-28` | Next/unallocated | `intended` |
| `S7-L144c` | Phase 1 should provide the holding pen, separate-half notifications, readiness notices and chase tools, replacing the spreadsheet as the control mechanism for incomplete intake. | `INT-32` | Next/unallocated | `intended` |
| `S7-L145a` | Phase 2 should create Pegasus jobs with automatic reference numbers and mapped instruction data in the structured single source. | `CASE-31` | Later/unallocated | `intended` |
| `S7-L145b` | Phase 2 should create the Box case folder and file case material automatically. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L145c` | Phase 2 should connect valuation APIs and DVLA lookup so external vehicle and valuation data are available when a job is born in Pegasus. | `EXT-07` | Later/unallocated | `intended` |
| `S7-L146a` | Phase 3 should expose the Engineer decision fields needed to record value, outcome, salvage and roadworthiness judgements. | `ENG-02` | Later/unallocated | `intended` |
| `S7-L146b` | Phase 3 should support the three repair-specification routes: vendor-neutral Send to AI first, Audatex import second and Glass’s when access is resolved, beginning with clear total losses. | `ENG-01` | Later/unallocated | `intended` |
| `S7-L146c` | Phase 3 should send generated reports using the applicable principal profile, completing the target report-send workflow. | `MAIL-17` | Later/unallocated | `intended` |
| `S7-L147a` | Phase 4 should add the query module and its durable query taxonomy. | `CASE-23` | Next/unallocated | `intended` |
| `S7-L147b` | Phase 4 should extend the general report engine with the listed addendum, diminution, audit and contract-repair assessment outputs as a compact report-suite increment. | `RPT-01` | Later/unallocated | `intended` |
| `S7-L147c` | The source expects these high-value report and query extensions to be comparatively small once the core is present. | `this change record provenance` | provenance | `accepted` |
| `S7-L148a` | Phase 5 should surface the accumulated per-engineer, per-principal and operational data through MI dashboards. | `MI-03` | Later/unallocated | `intended` |
| `S7-L148b` | Phase 5 should provide accounts and invoice generation from accumulated case and reporting data. | `EXT-11` | Later/unallocated | `intended` |
| `S7-L148c` | Basic individual logins should precede Phase 5, with full logins, fine-grained roles and superuser reporting completed in Phase 5. | `ACC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S8-L152a` | Whether direct Glass’s access will be refused or priced out must be resolved early. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S8-L152b` | The direct Glass’s estimating replacement is contingent on acceptable access and price. | `EXT-06` | Later/unallocated | `intended` |
| `S8-L152c` | Audatex import and Send to AI should mitigate the loss of the Glass’s route if direct integration is unavailable. | `ENG-01` | Later/unallocated | `intended` |
| `S8-L154a` | Budget, contracts and licensing terms for CAP, Glass’s and Cazana outside EVA must be confirmed. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S8-L154b` | The valuation replacement must accommodate the terms under which each supported valuation provider licenses direct use. | `EXT-07` | Later/unallocated | `intended` |
| `S8-L156a` | Every AI-produced assessment, response or report must be assigned to and reviewed and approved by a named Engineer before external release because the output carries expert responsibility. | `AI-09` | Later/unallocated | `intended` |
| `S8-L156b` | The vendor-neutral review transport must make approval explicit, logged and attributable before the approved proposal can leave Pegasus. | `AI-09` | Later/unallocated | `intended` |
| `S8-L158` | The design should cover UK GDPR basics for personal data and vehicle images flowing through email, request-scoped upload, AI processing and Box, including retention, access control and processor agreements. | `DOC-08` | Now/0.1.0-alpha.1 | `intended` |
| `S8-L160a` | EVA and Pegasus should run in parallel during migration, accepting temporary double-keying and moving one provider or job type at a time. | `EXT-03` | Now/0.1.0-alpha.1 | `intended` |
| `S8-L160b` | The deferred EVA replacement slice should shorten and ultimately end double-keying after each migrated flow is stable. | `CASE-22` | Later/unallocated | `intended` |
| `S8-L162` | Capacity planning should size phases honestly for one developer using AI assistance and retain EVA until each phase is genuinely stable. | `docs/product/areas/platform-and-operator-experience.md` | accepted authority | `intended` |
| `S8-L164` | The open wording blockers are salvage paragraphs for Categories N, A, B and N/A; the recovery-and-storage paragraph; final statement-of-truth wording; and qualifications for E Mawdsley and N O’Reilly. | `docs/product/open-decisions.md` | unallocated blocker | `intended` |
| `S9-L168` | The accepted planning volume is 1,000–1,200 jobs per month. | `docs/product/areas/platform-and-operator-experience.md` | accepted authority | `accepted` |
| `S9-L170` | Structural table header: Person \| Role. | `this change record provenance` | provenance | `accepted` |
| `S9-L172a` | Andrew is recorded as owner and head engineer. | `this change record provenance` | provenance | `accepted` |
| `S9-L172b` | Andrew is the initial Pegasus superuser/Administrator assignment. | `ACC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S9-L173` | Ed is recorded as Senior Engineer. | `this change record provenance` | provenance | `accepted` |
| `S9-L174` | Neil is recorded as Engineer. | `this change record provenance` | provenance | `accepted` |
| `S9-L175` | Patrick is recorded as Junior Engineer. | `this change record provenance` | provenance | `accepted` |
| `S9-L176` | Jake is recorded as Trainee Engineer. | `this change record provenance` | provenance | `accepted` |
| `S9-L177` | Ben is recorded as senior administration staff. | `this change record provenance` | provenance | `accepted` |
| `S9-L178` | Lisa is recorded as administration staff. | `this change record provenance` | provenance | `accepted` |
| `S9-L179` | Fay is recorded as part-time administration staff. | `this change record provenance` | provenance | `accepted` |
| `S9-L180` | Alex is recorded as Developer and Automations Engineer and as Pegasus build lead. | `this change record provenance` | provenance | `accepted` |
| `S9-L182a` | The operating end state should reduce administration to one person monitoring an exception queue and let Engineers focus on judgement rather than filing. | `docs/product/areas/platform-and-operator-experience.md` | accepted authority | `intended` |
| `S9-L182b` | Pegasus’s accepted product vision is to own the system and retain the ability to build newly identified ideas rapidly. | `docs/product/index.md` | accepted authority | `accepted` |

Crosswalk assertion: **148 atomic rows cover all 86 non-empty
source paragraphs, bullets, metadata rows and table rows; no source item is
unmapped.** The source also contains 21 heading/separator lines that carry
structure only and therefore require no capability disposition.

## Verification and evidence

- [ ] Canonical product documents and the capability inventory agree with every
  crosswalk row.
- [ ] Capability inventory contains 229 unique IDs: Now 128, Next 32, Later 40,
  Not planned 29.
- [ ] EVA examples retain the exact ordered 13 keys and all 12 Engineer screens
  have reviewed findings.
- [ ] The Pegasus source directory and duplicate EVA source directory are absent.
- [ ] Semantic-version, language, solution-boundary and provider-package checks
  pass.
- [ ] Main solution and each imported workspace pass their documented independent
  build/test/smoke route.
- [ ] Development-only Web smoke proves the Pegasus UI and health endpoints;
  no non-Development intake route is introduced.
- [ ] Exact-head independent review has no unresolved blocker or required finding.

## Outcome

Pending implementation and exact-head evidence. The pull request remains
unmerged from the agent workflow.
