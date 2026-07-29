# Change: Orient the repository around Pegasus

```yaml
id: 2026-07-27-pegasus-repository-orientation
type: decision
status: in_review
risk: high
created: 2026-07-27
updated: 2026-07-29
issue: https://github.com/collisionengineers/pegasus/issues/6
pull_request: https://github.com/collisionengineers/pegasus/pull/18
baseline: d0965e1264dadc8d9942ac54fd68a4b45fd06f28
target_release: 0.1.0-alpha.1
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
```

## Delivery series

| Order | Pull request | Scope |
| ---: | --- | --- |
| 01 | https://github.com/collisionengineers/pegasus/pull/8 | Establish Pegasus orientation governance |
| 02 | https://github.com/collisionengineers/pegasus/pull/9 | Cut over the runtime identity to Pegasus |
| 03 | https://github.com/collisionengineers/pegasus/pull/10 | Import the document extraction workspace |
| 04 | https://github.com/collisionengineers/pegasus/pull/11 | Import the secured report renderer workspace |
| 05 | https://github.com/collisionengineers/pegasus/pull/12 | Import the hardened AI Centre workspace |
| 06 | https://github.com/collisionengineers/pegasus/pull/13 | Import Agent Skills and complete workspace manifests |
| 07 | https://github.com/collisionengineers/pegasus/pull/14 | Preserve Pegasus history and EVA evidence |
| 08 | https://github.com/collisionengineers/pegasus/pull/15 | Normalize imported reference terminology |
| 09 | https://github.com/collisionengineers/pegasus/pull/16 | Orient canonical Pegasus documentation |
| 10 | https://github.com/collisionengineers/pegasus/pull/17 | Enforce Pegasus repository integration |
| 11 | https://github.com/collisionengineers/pegasus/pull/18 | Centralize repository documentation authority |

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

- Canonical intended behavior and activation gates are owned by [product requirements](../requirements.md); stable capability IDs, horizons, and exact release allocation are owned separately by the [capability inventory](../capabilities.md).
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
| `DOC-CON-011` | PdfPig remains the authoritative embedded-PDF extraction path for the current release. The legacy `cedocumentmapper` is not reused; a bespoke extractor may replace PdfPig only after separate hardening, caller proof and acceptance. |

## Scope

### Included

- Product capability and area updates for the atomic source claims below,
  including valuation, Engineer information, reports, targeted sending,
  correspondence, accounts/invoicing, management information and AI proposals.
- Consolidation of the exact EVA drag-and-drop examples and 12 Engineer screens.
- Clean active identity rename, SemVer/horizon conversion and retirement of loose
  root planning documents into history.
- Three independently buildable, non-caller workspaces imported from four
  sources, with agent skills merged under AI Centre, plus independent CI checks.
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

Release precedence now has one current owner in [Delivery
dependencies](../requirements.md#delivery-dependencies). The complete restored
prerequisite graph, `Next` parallel branches, rejoin evidence, and independently
gated `Later` continuations remain retained as source-labelled detail in the
[dependency-ordered delivery
roadmap](../history/plans/delivery-roadmap.md), with operator execution routed
through [Release dependency order](../operations.md#release-dependency-order).
The history route owns neither current behavior nor status, and the
[capability inventory](../capabilities.md) remains allocation-only.
The deferred renderer integration preserves only the headless rendering seam and
required service contracts. Activation decommissions every imported renderer GUI
feature because Pegasus owns the invoking UI, removes the standalone renderer MCPB,
and exposes any retained renderer tools through the global Pegasus MCP. The GUI and
MCPB remain in the source-only workspace solely as import/parity evidence until that
accepted cutover; they are excluded from the future runtime and deployment boundary.

## Atomic source crosswalk

`S<section>-L<line>` locators refer to the supplied Markdown source identified
by the SHA-256 in this record. The exact supplied Markdown and DOCX remain in the
review-only external paths
`C:/Users/Alex/Documents/requirementsdocs/main-docs/PegasusSystemPlan.md` and
`PegasusSystemPlan.docx`, with hashes
`4eb3a75ef7d9066184be94bca5612653e78f49b8f3d3c661dee6da08f1e6655b` and
`c86ea81d04314c1a88200b59bcf89037d6548c516af91779fe2d2bf068e21e29`.
They are intentionally absent from repository history and must be supplied to
the independent reviewer through that local permitted source boundary. A suffix
splits one source line only where its clauses have different durable owners.
Table headings and source metadata are retained as provenance rows; they are
not product requirements.

| Source | Normalized atomic claim | Durable owner / capability | Allocation | Evidence state |
| --- | --- | --- | --- | --- |
| `Stable-L0` | The crosswalk structure is locator, claim, durable owner, exact allocation, and evidence state. | `this change record` | provenance | `accepted` |
| `Smetadata-L1` | The source document title is “PEGASUS”. | `this change record` | provenance | `accepted` |
| `Smetadata-L3` | The source is a system plan for Collision Engineers Ltd. | `this change record` | provenance | `accepted` |
| `Smetadata-L5` | The plan was prepared for Andrew in the owner role, Alex in the developer role, and the CE team. | `this change record` | provenance | `accepted` |
| `Smetadata-L7` | The source is dated 26 July 2026 and has status “Draft for discussion”. | `this change record` | provenance | `accepted` |
| `S1-L11a` | Pegasus is Collision Engineers’ case-management and reporting product and is intended to become the place where jobs are created, assessed, reported, and tracked. | `docs/requirements.md` | accepted authority | `accepted` |
| `S1-L11b1` | Keep EVA and the current manual handoff available in parallel while Pegasus capabilities are introduced. | `EXT-03` | Now/0.1.0-alpha.1 | `intended` |
| `S1-L11b2` | Replace EVA inspection and report-preparation work only through the deferred replacement slice. | `CASE-22` | Later/unallocated | `intended` |
| `S1-L11b3` | Replace surrounding spreadsheets and manual steps with owner-controlled Pegasus workflows as their capabilities are accepted. | `docs/requirements.md` | accepted authority | `intended` |
| `S1-L11c` | Box remains the backing file store for Pegasus-managed case files. | `DOC-02` | Now/0.1.0-alpha.1 | `accepted` |
| `S1-L13` | Capture each job once as structured canonical data and render the assessment report, fee note, audit report, diminution report, addendum, query response, invoice, and management statistic from that source so outputs require no retyping and cannot disagree. | `CASE-31` | Later/unallocated | `intended` |
| `S1-L15a1` | Use relevant instruction evidence to propose extracted assessment inputs for operator review. | `AI-04` | Later/unallocated | `intended` |
| `S1-L15a2` | Use image and damage evidence to draft an assessment proposal. | `AI-05` | Next/unallocated | `intended` |
| `S1-L15a3` | Carry every AI assessment proposal through durable named-Engineer review and approval before outward use. | `AI-09` | Later/unallocated | `intended` |
| `S1-L15b` | Generate query-response proposals for engineer review and approval rather than allowing autonomous outward responses. | `AI-08` | Later/unallocated | `intended` |
| `S1-L15c` | An existing deterministic report-renderer source implements computed-once figures and stable output rendering, but it has no production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S2-L19` | The planning baseline is 1,000–1,200 jobs per month, at which volume the current workflow exhibits the three documented problem classes. | `docs/requirements.md` | accepted authority | `accepted` |
| `S2-L21` | The current EVA-era manual bundle consumes two to three administrators on spreadsheet logging, missing-half chasing, WhatsApp downloads, EVA uploads, manual Box-folder creation, and reference-number selection, with attendant error risk. | `docs/requirements.md` | accepted authority | `accepted` |
| `S2-L23` | The current EVA-era completion workflow makes Engineers export PDFs, file them in Box, find the original instruction email, send and delete messages, and mark jobs complete after their expert work is finished. | `docs/requirements.md` | accepted authority | `accepted` |
| `S2-L25a` | EVA vendor dependence currently delays or prevents owner-controlled product changes. | `docs/requirements.md` | accepted authority | `accepted` |
| `S2-L25b` | Support an Engineer-selected contract-repair target in the canonical repair specification. | `ENG-01` | Later/unallocated | `intended` |
| `S2-L25c` | Generate a diminution report from accepted case data and the Engineer-entered percentage. | `RPT-04` | Later/unallocated | `intended` |
| `S2-L25d` | Offer a vendor-neutral AI assessment action through the durable proposal and review contract. | `AI-09` | Later/unallocated | `intended` |
| `S2-L27` | Pegasus is intended to turn product changes into owner-controlled work and shift administration from routine entry to exception monitoring. | `docs/requirements.md` | accepted authority | `accepted` |
| `S3-L31a1` | Accept structured instruction intake through the principal-scoped provider API. | `API-01` | Next/unallocated | `intended` |
| `S3-L31a2` | Accept non-API instruction intake through the single instructions inbox. | `INT-02` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L31a3` | Read the instruction source and expose extracted details for review. | `INT-19` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L31a4` | Allocate the next reference from the shared principal/year sequence. | `CASE-07` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L31a5` | Create the job only from definitive authorised intake. | `INT-25` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L31a6` | Request creation of the job's Box case folder. | `DOC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L31a7` | Place the new instruction-backed job in the held/not-ready workflow pending its image half. | `CASE-16` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L31b` | Automatically pair each arriving image set with its instruction-backed job regardless of whether images arrive through a request-scoped upload link or the occasional manual WhatsApp fallback. | `INT-28` | Next/unallocated | `intended` |
| `S3-L31c` | Notify the team as soon as both job halves are present and mark the job complete and ready to proceed. | `INT-32` | Next/unallocated | `intended` |
| `S3-L33a1` | Obtain valuation observations through supported CAP, Glass’s, and Cazana adapters. | `EXT-13` | Later/unallocated | `intended` |
| `S3-L33a2` | Retain valuation observations, mileage, Engineer notes, source versions, and the Engineer's accepted valuation decision. | `EXT-10` | Later/unallocated | `intended` |
| `S3-L33a3` | Require accepted valuation evidence and notes at the configurable readiness gate where applicable. | `CASE-14` | Now/0.1.0-alpha.1 | `intended` |
| `S3-L33b` | Arrange the engineer workbench so a ready job can move through a repair-specification route, expert decisions covering value, deductions, outcome, category, salvage, and roadworthiness, and report generation. | `UI-15` | Later/unallocated | `intended` |
| `S3-L33c` | Provide one target-report action that replies on the original instruction thread, applies the required recipients, triggers filing, completes the job, and records the management event. | `MAIL-17` | Later/unallocated | `intended` |
| `S3-L35a` | Use the later case query and held job data to produce a CE-house-style response proposal for engineer approval. | `AI-08` | Later/unallocated | `intended` |
| `S3-L35b` | Generate any required diminution report from the data already held for the job. | `RPT-04` | Later/unallocated | `intended` |
| `S3-L35c` | Generate any required addendum from the data already held for the job. | `RPT-05` | Later/unallocated | `intended` |
| `S4.1-L41a` | Treat an instruction and an image set as order-independent job halves, place each arrival in a holding pen, and pair the halves automatically when both are available. | `INT-28` | Next/unallocated | `intended` |
| `S4.1-L41b` | Keep every unpaired item visible with its age and chase status, and notify the team when the second half makes the job complete. | `INT-32` | Next/unallocated | `intended` |
| `S4.1-L43` | Constrain supported intake to the three declared channels rather than allowing an open-ended channel set. | `docs/requirements.md` | accepted authority | `intended` |
| `S4.1-L45` | Accept structured instructions from larger work providers through a provider API without email-reading steps. | `API-01` | Next/unallocated | `intended` |
| `S4.1-L47a` | Ingest non-API work from the single instructions inbox. | `INT-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L47b` | Retain each instruction email and its attachments under a stable source occurrence. | `INT-09` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L47c1` | Read mapped PDF text and embedded images from supported instructions. | `INT-11` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L47c2` | Expose extracted instruction values in a typed operator-reviewable draft. | `INT-19` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L47c3` | Preserve field provenance, validation, missing values, and contradictions for mapped instruction values. | `INT-20` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L47d` | Populate typed client, accident-date, and principal case data only from accepted extracted values. | `CASE-11` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L49a` | Give clients, bodyshops, and storage yards a request-scoped web link through which they can upload images. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L49b` | Do not create a persistent external client, bodyshop, or storage-yard portal; the upload-link capability is the scoped exception. | `BND-06` | Not planned/unallocated | `accepted` |
| `S4.1-L49c1` | Retain staff handling of WhatsApp images only as an occasional manual fallback. | `EXT-14` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L49c2` | Submit manually downloaded WhatsApp images through the request-scoped upload flow. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L51a` | Allow staff to send a request-scoped upload link to a client. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S4.1-L51b` | Allow staff to log a bodyshop chase and see which half each incomplete job is awaiting. | `INT-32` | Next/unallocated | `intended` |
| `S4.2-L55a1` | Automate unique reference allocation through the shared principal/year sequence. | `CASE-07` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55a2` | Create the job only from definitive authorised intake. | `INT-25` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55a3a` | Populate typed case data from accepted mapped-instruction values, including mileage where present. | `CASE-11` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55a3b` | Allow administrators to record the Engineer's roadworthiness and repairable/total-loss findings, including the supplied examples of unroadworthiness or a customer preference for total loss. | `CASE-28` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55a4a` | Require both accepted job halves at the configurable readiness gate. | `CASE-14` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55a4b` | Transition the job from its held/not-ready state after the readiness gate passes. | `CASE-16` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55b1` | Create the case Box folder as part of automated setup. | `DOC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55b2` | File the instruction, images, and notes into the Box case folder as part of automated setup. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L55c1` | Provide external adapters for CAP, Glass’s, and Cazana guide-value retrieval, including optional prefetch before a user opens the job. | `EXT-13` | Later/unallocated | `intended` |
| `S4.2-L55c2` | Provide DVLA registration lookup for make, model, year, engine, and fuel, including optional prefetch before a user opens the job. | `EXT-01` | Now/0.1.0-alpha.1 | `intended` |
| `S4.2-L57` | Design queue operations so one team member can monitor and resolve unmatched images, unmapped instructions, and unusual cases instead of requiring two to three people for data entry. | `docs/requirements.md` | accepted authority | `intended` |
| `S4.3-L61` | Arrange the engineer workspace around expert decisions and expose exactly the three declared repair-specification entry routes without unrelated administration. | `UI-15` | Later/unallocated | `intended` |
| `S4.3-L63a` | Support Glass’s as a repair-specification route equivalent to the traditional EVA-integrated route. | `ENG-01` | Later/unallocated | `intended` |
| `S4.3-L63b` | Glass’s integration is an external dependency that must remain explicit until its access and wording are resolved. | `docs/open-decisions.md` | unallocated blocker | `accepted` |
| `S4.3-L65` | Support an Audatex route in which the estimate is built in Audatex, printed to PDF, attached to the job, and mapped into Pegasus’s standard repair-specification format. | `EXT-12` | Later/unallocated | `intended` |
| `S4.3-L67a1` | Propose an image-based repair specification, including for clear-total-loss cases. | `AI-05` | Next/unallocated | `intended` |
| `S4.3-L67a2` | Allow an approved AI proposal to become the canonical repair specification with route provenance. | `ENG-01` | Later/unallocated | `intended` |
| `S4.3-L67b` | Carry the AI repair-specification work through a durable vendor-neutral request, lease, proposal, and review contract rather than a direct model API. | `AI-09` | Later/unallocated | `intended` |
| `S4.3-L67c` | An existing AI/skills source implements relevant clear-total-loss assessment behavior, but it has no production caller. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S4.3-L69a1` | Allow the Engineer to set a contract-repair target in the canonical repair specification, for example 80% of pre-accident value. | `ENG-01` | Later/unallocated | `intended` |
| `S4.3-L69a2` | Propose an image-based assessment targeted to the selected contract-repair cap without repeated manual adjustment. | `AI-05` | Next/unallocated | `intended` |
| `S4.3-L69b` | Submit and review the capped assessment through the vendor-neutral “Send to AI” transport contract. | `AI-09` | Later/unallocated | `intended` |
| `S4.3-L71a` | Require the engineer to complete final value and deductions, outcome from total loss/repairable/cash in lieu/contract repair, salvage category and value, and roadworthiness with reason. | `ENG-02` | Later/unallocated | `intended` |
| `S4.3-L71b` | Compose or compute every derivable report value and narrative, including settlement figures, rather than asking the engineer to retype them. | `RPT-01` | Later/unallocated | `intended` |
| `S4.4-L75a1` | Produce the core assessment through a deterministic engine with computed-once figures, validation before render, and the fixed CE layout. | `RPT-01` | Later/unallocated | `intended` |
| `S4.4-L75a2` | Render the four assessment outcome variants and include the fee-note page. | `RPT-02` | Later/unallocated | `intended` |
| `S4.4-L75b` | The existing report-renderer source embodies the July 2026 deterministic-generation design but has no production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S4.4-L75c` | The fixed CE visual and house-style assets are governed by the design authority. | `design/` | accepted authority | `accepted` |
| `S4.4-L77a` | Render a conservative report and a maximised audit report from one job containing two repair specifications. | `RPT-03` | Later/unallocated | `intended` |
| `S4.4-L77b1` | Calculate and record the uplift between the conservative and maximised Audit specifications. | `RPT-03` | Later/unallocated | `intended` |
| `S4.4-L77b2` | Surface the recorded Audit uplift as a per-Engineer management measure. | `MI-01` | Later/unallocated | `intended` |
| `S4.4-L79` | Generate a diminution-in-value report from the original job data after the engineer supplies the percentage. | `RPT-04` | Later/unallocated | `intended` |
| `S4.4-L81` | Generate an addendum from existing job data with the amendment applied and without retyping the job. | `RPT-05` | Later/unallocated | `intended` |
| `S4.4-L83a` | Generate the fee note and itemised repair-specification breakdown as report-engine outputs. | `RPT-02` | Later/unallocated | `intended` |
| `S4.4-L83b` | File the fee note and itemised repair-specification breakdown in Box alongside the report. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.5-L87a` | Send the report and fee note from Pegasus on the original instruction email thread or API route, using each principal’s saved CC suggestions, delivery preferences such as separate report and image attachments, and standing notes; then mark the job complete. | `MAIL-17` | Later/unallocated | `intended` |
| `S4.5-L87b` | File the sent report-package items into Box. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.5-L87c1` | Record report-package sending, job completion, and the management event as part of the idempotent report-send transaction. | `MAIL-17` | Later/unallocated | `intended` |
| `S4.5-L87c2` | Consume the recorded report-send/completion event in operational management measures. | `MI-03` | Later/unallocated | `intended` |
| `S4.6-L91a` | Keep the case alive after reporting and display its complete email chain and correspondence history on the job. | `MAIL-11` | Next/unallocated | `intended` |
| `S4.6-L91b` | For a defendant-engineer challenge, dispute, requested adjustment, or similar query, propose a job-aware CE-house-style letterhead response for the engineer to accept or amend before sending. | `AI-08` | Later/unallocated | `intended` |
| `S4.6-L91c` | The existing AI/skills source contains the earlier cost-defence rebuttal format and house-style rules, but it has no production caller. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S4.6-L93` | Tag every query with a maintained taxonomy, including supplementary request, repair-cost challenge, and valuation dispute, so query-type training statistics can be produced. | `CASE-23` | Next/unallocated | `intended` |
| `S4.7-L97` | Pegasus should track every practicable event so management statistics are produced as a by-product of normal case activity rather than through separate manual work. | `docs/requirements.md` | accepted authority | `intended` |
| `S4.7-L99` | Per-engineer MI should show reports per day, jobs completed, query rate, query types and audit-report uplift, and support coaching using patterns such as supplementaries from missed hidden damage or excessive repair-cost challenges. | `MI-01` | Later/unallocated | `intended` |
| `S4.7-L101` | Per-principal MI should show report counts by type and period and feed those figures directly into invoice generation. | `MI-02` | Later/unallocated | `intended` |
| `S4.7-L103` | Operational MI should show holding-pen age, instruction-to-images time, ready-to-sent time and overall turnaround. | `MI-03` | Later/unallocated | `intended` |
| `S4.7-L105a` | Every engineer and administration team member should have an individual login. | `ACC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S4.7-L105b1` | Retain the role-restricted visibility boundary for accounts information. | `EXT-11` | Later/unallocated | `intended` |
| `S4.7-L105b2` | Enforce the superuser-only boundary for accounts information and management statistics on every page and action. | `ACC-04` | Now/0.1.0-alpha.1 | `intended` |
| `S4.7-L105c` | Andrew is the initial superuser/Administrator assignment. | `ACC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.8-L109a` | Box should remain the archive and file store behind Pegasus. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S4.8-L109b` | Pegasus should create each case folder through the Box API. | `DOC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S4.8-L109c` | Pegasus should automatically file the instruction, images, notes, reports, fee notes, breakdowns and sent correspondence so staff do not handle Box manually and it serves as the audit-proof case library. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S5-L113` | The change record introduces the following items as substantial existing Pegasus assets, without establishing production deployment. | `this change record provenance` | provenance | `accepted` |
| `S5-L115` | Structural table header: Asset \| What it gives Pegasus. | `this change record provenance` | provenance | `accepted` |
| `S5-L117a` | The imported report generator contains deterministic PDF rendering, computed-once figures, validation, four outcome variants, a fee-note page and a variables walkthrough mapping fields to dashboard input types; it has no established production caller. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S5-L117b` | The source records the report generator status as locked in July 2026. | `this change record provenance` | provenance | `accepted` |
| `S5-L118a` | The imported renderer contains a structured JSON job schema as non-caller candidate/consumer evidence; it does not define the Core data model. | `workspaces/report-renderer` | implemented/non-caller | `implemented` |
| `S5-L118b` | Pegasus should use one accepted structured case/engineering record as the source for deterministic downstream outputs. | `CASE-31` | Later/unallocated | `intended` |
| `S5-L119a` | An imported image-to-structured-assessment workflow exists and works for clear total-loss examples, but no production caller or direct model API is established. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S5-L119b` | The clear-total-loss structured assessment outcome should become one of the Engineer repair-specification routes exposed as vendor-neutral Send to AI. | `ENG-01` | Later/unallocated | `intended` |
| `S5-L119c` | Send to AI should use the durable vendor-neutral request, lease, proposal and review transport contract. | `AI-09` | Later/unallocated | `intended` |
| `S5-L120a` | An imported cost-defence skill can produce court-addressed cost-justification documents in a fixed house style, but it has no established production caller. | `workspaces/ai-centre` | implemented/non-caller | `implemented` |
| `S5-L120b` | The query-response proposal workflow should use the cost-defence capability as the basis for formal query responses. | `AI-08` | Later/unallocated | `intended` |
| `S5-L121` | The codified CE house-style authority defines tone, wording and banned terms for outbound letters, emails and rebuttals drafted with AI assistance. | `design/` | accepted authority | `accepted` |
| `S5-L122` | The existing CE design assets provide brand tokens, fonts, letterhead and document layout for Pegasus screens and outputs. | `design/` | accepted authority | `implemented` |
| `S6-L126` | External-dependency conversations should begin immediately because their answers block or shape build decisions. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L128` | Structural table header: Dependency \| What we need to find out. | `this change record provenance` | provenance | `accepted` |
| `S6-L130a` | The team must resolve whether Glass’s repair estimating can be integrated directly outside EVA, including licensing, API or embedded access and cost. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L130b` | A direct Glass’s estimating replacement should be provided only if access and commercial terms are viable. | `EXT-06` | Later/unallocated | `intended` |
| `S6-L130c` | If Glass’s is unavailable, Engineers should retain the Audatex-import and Send-to-AI repair-specification routes. | `ENG-01` | Later/unallocated | `intended` |
| `S6-L131a` | API access, licensing and terms for CAP, Glass’s and Cazana outside EVA must be confirmed. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L131b` | Pegasus should replace EVA-mediated valuation access with supported direct valuation integrations where terms permit. | `EXT-07` | Later/unallocated | `intended` |
| `S6-L132` | The provider API capability must establish which larger work providers can submit instructions by API and which formats they use. | `API-01` | Next/unallocated | `intended` |
| `S6-L133a1` | Box API access should support case-folder creation. | `DOC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S6-L133a2` | Box API access should support automated filing. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S6-L133b` | The source treats the Box API as mature and the proposed integration as straightforward. | `this change record provenance` | provenance | `accepted` |
| `S6-L134a` | An external adapter should provide DVLA vehicle-detail lookup. | `EXT-01` | Now/0.1.0-alpha.1 | `intended` |
| `S6-L134b` | A provider lookup such as Experian AutoCheck should support the mandatory vehicle-history check. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S6-L135` | The Audatex integration must confirm that its PDF mapping covers the variants Engineers produce so drag-in import is reliable. | `EXT-12` | Later/unallocated | `intended` |
| `S7-L139a1` | Each build phase should be independently useful. | `docs/capabilities.md` | accepted authority | `intended` |
| `S7-L139a2` | Keep the current manual EVA handoff available in parallel during phased delivery. | `EXT-03` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L139b` | After Pegasus covers the full flow, work should migrate from EVA provider by provider through a deferred replacement slice. | `CASE-22` | Later/unallocated | `intended` |
| `S7-L139c` | The source assigns Alex as build lead and records AI-assisted development throughout the build. | `this change record provenance` | provenance | `accepted` |
| `S7-L141` | Structural table header: Phase \| Build \| Why this order. | `this change record provenance` | provenance | `accepted` |
| `S7-L143a` | Phase 0 should run dependency enquiries before build decisions, especially for Glass’s and valuation APIs. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S7-L143b` | Phase 0 should confirm the structured job data model, using the substantially completed report specification as its basis. | `CASE-31` | Later/unallocated | `intended` |
| `S7-L144a` | Phase 1 should provide a request-scoped image-upload portal that can operate while cases continue through EVA. | `INT-31` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L144b1` | Phase 1 should automate the single instructions inbox. | `INT-02` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L144b2` | Phase 1 should pair received instructions and images into jobs. | `INT-28` | Next/unallocated | `intended` |
| `S7-L144c` | Phase 1 should provide the holding pen, separate-half notifications, readiness notices and chase tools, replacing the spreadsheet as the control mechanism for incomplete intake. | `INT-32` | Next/unallocated | `intended` |
| `S7-L145a1` | Phase 2 should create Pegasus jobs only from definitive authorised intake. | `INT-25` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L145a2` | Phase 2 should allocate automatic references through the shared principal/year sequence. | `CASE-07` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L145a3` | Phase 2 should populate mapped instruction data in the typed structured case record. | `CASE-11` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L145a4` | Phase 2 should preserve one accepted structured record as the source for downstream outputs. | `CASE-31` | Later/unallocated | `intended` |
| `S7-L145b1` | Phase 2 should create the Box case folder automatically. | `DOC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L145b2` | Phase 2 should file case material into Box automatically. | `DOC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L145c1` | Phase 2 should connect supported valuation-source adapters so external valuation observations are available when a job is born in Pegasus. | `EXT-13` | Later/unallocated | `intended` |
| `S7-L145c2` | Phase 2 should connect DVLA lookup so external vehicle data is available when a job is born in Pegasus. | `EXT-01` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L146a` | Phase 3 should expose the Engineer decision fields needed to record value, outcome, salvage and roadworthiness judgements. | `ENG-02` | Later/unallocated | `intended` |
| `S7-L146b` | Phase 3 should support the three repair-specification routes: vendor-neutral Send to AI first, Audatex import second and Glass’s when access is resolved, beginning with clear total losses. | `ENG-01` | Later/unallocated | `intended` |
| `S7-L146c` | Phase 3 should send generated reports using the applicable principal profile, completing the target report-send workflow. | `MAIL-17` | Later/unallocated | `intended` |
| `S7-L147a` | Phase 4 should add the query module and its durable query taxonomy. | `CASE-23` | Next/unallocated | `intended` |
| `S7-L147b1` | Phase 4 should extend the report engine with addendum output. | `RPT-05` | Later/unallocated | `intended` |
| `S7-L147b2` | Phase 4 should extend the report engine with diminution output. | `RPT-04` | Later/unallocated | `intended` |
| `S7-L147b3` | Phase 4 should extend the report engine with audit output. | `RPT-03` | Later/unallocated | `intended` |
| `S7-L147b4` | Phase 4 should extend the canonical repair specification with the contract-repair target and outcome. | `ENG-01` | Later/unallocated | `intended` |
| `S7-L147c` | The source expects these high-value report and query extensions to be comparatively small once the core is present. | `this change record provenance` | provenance | `accepted` |
| `S7-L148a1` | Phase 5 should surface accumulated per-Engineer data through an MI dashboard. | `MI-01` | Later/unallocated | `intended` |
| `S7-L148a2` | Phase 5 should surface accumulated per-principal data through an MI dashboard. | `MI-02` | Later/unallocated | `intended` |
| `S7-L148a3` | Phase 5 should surface accumulated operational data through an MI dashboard. | `MI-03` | Later/unallocated | `intended` |
| `S7-L148b` | Phase 5 should provide accounts and invoice generation from accumulated case and reporting data. | `EXT-11` | Later/unallocated | `intended` |
| `S7-L148c1` | Individual staff sign-in should be available before Phase 5. | `ACC-01` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L148c2` | Phase 5 should complete the Administrator, Engineer, and User role model. | `ACC-02` | Now/0.1.0-alpha.1 | `intended` |
| `S7-L148c3` | Phase 5 should apply fine-grained role protection, including the superuser-only reporting boundary. | `ACC-04` | Now/0.1.0-alpha.1 | `intended` |
| `S8-L152a` | Whether direct Glass’s access will be refused or priced out must be resolved early. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S8-L152b` | The direct Glass’s estimating replacement is contingent on acceptable access and price. | `EXT-06` | Later/unallocated | `intended` |
| `S8-L152c` | Audatex import and Send to AI should mitigate the loss of the Glass’s route if direct integration is unavailable. | `ENG-01` | Later/unallocated | `intended` |
| `S8-L154a` | Budget, contracts and licensing terms for CAP, Glass’s and Cazana outside EVA must be confirmed. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S8-L154b` | The valuation replacement must accommodate the terms under which each supported valuation provider licenses direct use. | `EXT-07` | Later/unallocated | `intended` |
| `S8-L156a` | Every AI-produced assessment, response or report must be assigned to and reviewed and approved by a named Engineer before external release because the output carries expert responsibility. | `AI-09` | Later/unallocated | `intended` |
| `S8-L156b` | The vendor-neutral review transport must make approval explicit, logged and attributable before the approved proposal can leave Pegasus. | `AI-09` | Later/unallocated | `intended` |
| `S8-L158a` | Apply role-based access protection to personal data and vehicle images across email, request-scoped upload, AI processing and Box flows. | `ACC-04` | Now/0.1.0-alpha.1 | `intended` |
| `S8-L158b` | Resolve and record the retention rules for personal data and vehicle images before activating each external flow; this does not create an automated retention workflow. | [Requirements](../requirements.md#quality-capacity-security-and-evidence) | accepted authority | `intended` |
| `S8-L158c` | Confirm applicable processor terms before activating any external email, upload, AI or Box processor. | [Requirements](../requirements.md#quality-capacity-security-and-evidence) | accepted authority | `intended` |
| `S8-L160a1` | EVA and the manual Pegasus handoff should run in parallel during migration, accepting temporary double-keying. | `EXT-03` | Now/0.1.0-alpha.1 | `intended` |
| `S8-L160a2` | Move one provider or job type at a time through the deferred EVA-replacement migration. | `CASE-22` | Later/unallocated | `intended` |
| `S8-L160b` | The deferred EVA replacement slice should shorten and ultimately end double-keying after each migrated flow is stable. | `CASE-22` | Later/unallocated | `intended` |
| `S8-L162` | Capacity planning should size phases honestly for one developer using AI assistance and retain EVA until each phase is genuinely stable. | `docs/requirements.md` | accepted authority | `intended` |
| `S8-L164` | The open wording blockers are salvage paragraphs for Categories N, A, B and N/A; the recovery-and-storage paragraph; final statement-of-truth wording; and qualifications for E Mawdsley and N O’Reilly. | `docs/open-decisions.md` | unallocated blocker | `intended` |
| `S9-L168` | The accepted planning volume is 1,000–1,200 jobs per month. | `docs/requirements.md` | accepted authority | `accepted` |
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
| `S9-L182a` | The operating end state should reduce administration to one person monitoring an exception queue and let Engineers focus on judgement rather than filing. | `docs/requirements.md` | accepted authority | `intended` |
| `S9-L182b` | Pegasus’s accepted product vision is to own the system and retain the ability to build newly identified ideas rapidly. | `docs/requirements.md` | accepted authority | `accepted` |

Crosswalk assertion: **203 atomic rows cover all 86 non-empty
source paragraphs, bullets, metadata rows and table rows; no source item is
unmapped.** The source also contains 21 heading/separator lines that carry
structure only and therefore require no capability disposition.

## EVA source reconciliation

The retired source directory remains available to exact-head review at
`C:/Users/Alex/Documents/requirementsdocs/eva_information/`. The following
SHA-256 inventory proves each source disposition. Same-name screenshots and
`eva_information.md` are byte-identical; the drag/drop example is the retained
`Final Format Example 02.json`; `AX_SP58WVO.json` is the unique imported case;
and the differing screenshot-findings note was deliberately consolidated into
the retained canonical note rather than discarded.

| Source relative path | Retained relative path | Source SHA-256 | Retained SHA-256 / disposition |
| --- | --- | --- | --- |
| `AX_SP58WVO.json` | `AX_SP58WVO.json` | `4daccb2e92b8699d1ae642eee48706c8166588fc903578b2710004657c55ac9a` | identical |
| `eva_drag_drop_json_example.json` | `Final Format Example 02.json` | `1fa894616fc688cd6c55cbbbec5ef26cad118124b87d54dc45d50db26578a574` | identical |
| `eva_information.md` | `eva_information.md` | `0c9cc67a76831c1b00b6a8f16ab07b1166f0028e36db01856181c63c9100f974` | identical |
| `eva_screenshot_findings.md` | `eva_screenshot_findings.md` | `df44e97a9bfa4f1829f2828231d62ce0191f45da5309c9a447df2a0b3a64eb8b` | consolidated: `8be21154af0a3d2f49194f4b2d3d699d9fe9672d4866cea45f0c03df6caf9139` |
| `screenshots/engineer-screens/engineer1.png` | same | `f895d1ee8799cb06c065e902f036c75d6d92268d16a354483f2403a12914f093` | identical |
| `screenshots/engineer-screens/engineer2.png` | same | `eabd4fee2aecc200d05797ae11fba63766559ffce33acd2fbaf6341f0b4d55bc` | identical |
| `screenshots/engineer-screens/engineer3.png` | same | `e82526782c6fb20d32bdb0271817036db15727589826745f45ddd263db0bc2ec` | identical |
| `screenshots/engineer-screens/engineer4.png` | same | `e82526782c6fb20d32bdb0271817036db15727589826745f45ddd263db0bc2ec` | identical |
| `screenshots/engineer-screens/engineer5.png` | same | `e7bae8c076b4c2f6f37e34ccc5f25035b05f56b1d1e93fa7fda2eb5155d2a6b1` | identical |
| `screenshots/engineer-screens/engineer6.png` | same | `6680723b31f8f14816ea368aecb2943876c30effcc0f55d52844f46e4b1fc985` | identical |
| `screenshots/engineer-screens/engineer7.png` | same | `d7aca2fa8c13bd472172d6f522c2e715b5f6e8f2e3dbf0f835a00d38f1efc2af` | identical |
| `screenshots/engineer-screens/engineer8.png` | same | `cfcae81c9ec415f299f74c90a1b4d8b72e6e2047ad50361a8be80c6410769ce6` | identical |
| `screenshots/engineer-screens/engineer9.png` | same | `5c8cc8c5b60db3f4e95a90e6cbc7c33f425bc625210b2e059212ca6de279aabd` | identical |
| `screenshots/engineer-screens/engineer10.png` | same | `1a815703c93b1c76a813db124c352587003465e17c6b47f0b98f4b21f7e9d348` | identical |
| `screenshots/engineer-screens/engineer11.png` | same | `d740c8d2d4fa8d08315d9f21a7e44c6905f99f5eb0301232c755e1231559b7e8` | identical |
| `screenshots/engineer-screens/engineer12.png` | same | `864dd7129192a843c3baf6e6f180bc5ef3cf619bd0ebeda8b2ff0a77eefddfa2` | identical |
| `screenshots/{0E6CBDDD-7C09-4088-A2F7-35C9041AAA42}.png` | same | `c3610343f557e9f48378197698fea87830c49aa6a4d14a33d4e437eac419d4c7` | identical |
| `screenshots/{245DB80D-0EB3-42CF-9775-2CD24CEDF88A}.png` | same | `82af91d5523129ecfd627f91dab5dbb9d35ee40d2a9bde01c4d6a232207ea9f3` | identical |
| `screenshots/{28E72E59-7EE2-43DD-AA15-2F5E53DBCF6E}.png` | same | `2d35d51bd8b7c5f9048f0211c9682f6a8c8d4272f70c0b595cd87395b0b98c38` | identical |
| `screenshots/{549C62EE-3D5E-4ADD-9F1A-714D4BBD46B9}.png` | same | `e9ec2dd6a1353cc39927b65cbc53b29214b877e9ce780a6bd10a01fc556a5b71` | identical |
| `screenshots/{93A1970E-71A7-48D5-B940-4CE4B98228B1}.png` | same | `e5ed70fb27f4d26b972d93ceeaf761487b11fe16fed8109ab90d46d7c3ce6bf2` | identical |
| `screenshots/{94D6ED5C-348F-4E85-B941-ECB12AE1814C}.png` | same | `ac8b17110c8d5653be42db841a2ee02891e16162cd1cdf841f1bf92a72433e5e` | identical |
| `screenshots/{9666FEB7-AFB3-499F-9518-9AA5205CE954}.png` | same | `dc838cde8768ede00f3261d5f4c0ce61e647b987a17169445864a1a11bc9d655` | identical |
| `screenshots/{9A82B1E4-2A4F-4B5E-8686-3C2F82E567F1}.png` | same | `b74e0670dd5812532fef3118b453ea458c3dc90991f2cce7b984070bce2d7989` | identical |
| `screenshots/{AF221409-D318-4F27-875F-12DEB9FA879E}.png` | same | `3d7a8dd96cf7573b975b6731641562afa6f50cf512d8b06d8a82b4873ec6fed0` | identical |
| `screenshots/{B2206742-2C06-49AD-9E78-F2047E4F9220}.png` | same | `5775780515fa485371ec203e74c57d6a17b7c3e2c896eddf93e16aa2b5bbf2c2` | identical |
| `screenshots/{B64D5E21-E7D4-44BC-A66A-2A42AF8A69C0}.png` | same | `0164b7d7f406417c677b6941e6a91276c1369bf2a9c4587279c6ce52a28347d1` | identical |
| `screenshots/{C292430C-514C-4073-AECA-63E4F8D0ED78}.png` | same | `d80d6cc0fccbd8adcb36dae73a64941e4548809a0419288c3c4c39f980cef70a` | identical |
| `screenshots/{CCD7E916-7A98-488D-BDDE-8E85F7C9063F}.png` | same | `7114ff7e82e56c4e73ea84129c230387a2932bac81c67b8a6cf144feb490f713` | identical |
| `screenshots/{D40D4374-F9FA-46DA-A042-6DDE90C00D6D}.png` | same | `4e2e552358eec7c6d9f5d5cb27a35f3e01d10faa285e0770fb0e6ecc82f4e48f` | identical |
| `screenshots/{D9450032-911C-4ED4-BF53-669B626D33DE}.png` | same | `72dbab35bc92b9a1c8f9921159417a410fe204f6231a55a99c8abf37d1b41fda` | identical |

## Independent review remediation

Pull request review validated and remediated the following required findings:

- corrected allocation sequencing, provider-package terminology, retained historical wording,
  reference-report boundaries, and duplicate capability-ID enforcement;
- restricted workspace policy scans to tracked source and added the Windows long-path checkout
  prerequisite;
- aligned document-extraction CI with locked restore and Microsoft Testing Platform, and made
  output bundles materialize assets from nested extraction results;
- removed imported renderer sample case data, its unusable parity gate, and duplicated valuation
  business policy; generated Core-owned starter drafts now provide the diagnostic rendering path;
- aligned the renderer container with its Playwright package, blocked remote image retrieval,
  bounded multipart array indices, trusted only request-created attachment paths, and returned
  client errors for unknown templates;
- preserved configured OIDC issuer validation, staged-upload lifetime and post-commit durability,
  and added renewable ingestion-job leases with interrupted-work reclamation.
- regenerated every changed imported-source manifest from final committed Git blobs;
- bounded renderer browser acquisition and page creation by caller cancellation, restricted
  bundled signature resolution to known keys, reported the actual de-collided artifact filename,
  and returned per-item validation for null batch entries.

## Approved documentation consolidation

The documentation-centralization plan is identified by SHA-256
`9efd6e39b6f01dfbb449e8d0f39533b63b1cac2f5b6120cd7c63e4428fcb66d7`.
It is a planning input, not proof of the implemented result.

Independent review invalidated the plan's claimed 512-artifact baseline and the
first implementation's no-information-loss conclusion. The census included a
nonexistent predecessor-Web jquery-validation licence path; that file is absent
from baseline commit
`467284f23b268e199d7fbe77dbb2163b50f00e23`. The temporary 512-row
disposition, material-claim, and callsite files therefore are not accepted
evidence and are not published as repository truth.

The same review found unique source evidence and still-current planning
dependencies removed, protected imported skill packages rewritten, and several
material product/operator rules weakened or left without a canonical owner.
The remediation consequently:

- restores unique reports, evaluation provenance, the dependency-ordered
  delivery plan, the mailbox decision dossier, and the operator questionnaire;
- restores every imported AI skill package, package-local reference, and
  `dev-ref/` maintenance source to its baseline bytes, with only the enclosing
  Pegasus source-boundary index remaining repository-owned;
- centralises the settled mailbox taxonomy, source/dispatch identity, Triage,
  case lifecycle, access, request-scoped upload, external-workflow, report, and
  recovery rules in the canonical owners;
- keeps historical evidence source-labelled and reachable without promoting it
  to current implementation, deployment, or acceptance evidence; and
- replaces obsolete caller and product references with Pegasus terminology
  while retaining explicit CollisionSpike predecessor provenance.

No repository change is made under `docs/reference/imp-docs/`, and no claim in
this remediation relies on that subtree.

### Accepted contradiction resolutions

| IDs | Resolution |
| --- | --- |
| DOC-CON-012–016 | TRI-04 uses independently optional findings with at least one populated; operations owns tool profiles and planned evaluator status; `docs/index.md` alone owns authority order. |
| DOC-CON-017–021 | Corpus counts remain dated/scope-qualified; old exact-head evidence is invalidated; provider/domain terminology is exact; QDOS owns one change record; active repository workflow is owned by the installed `.agents/skills/` workflows and configured in `docs/agents/`, with superseded plugin migration retained only as change provenance. |
| DOC-CON-022–027 | `MAIL-12` remains deferred; the user-confirmed Received/Sent/Reply taxonomy is immutable in `docs/requirements.md`, while predicates and activation remain unresolved; ADR-0005 occurrence identity wins; dormant OCR is removed; historical readiness wording is deleted; cedocumentmapper is predecessor evidence only. |
| DOC-CON-028–033 | Desktop-only location evidence outranks defaults; image-led intake remains pre-case; Box custody is distinct from staging; Operations-first is selected; route catalog is design, not caller proof; alpha has no OCR replacement. |
| DOC-CON-034–040 | AI packages are evidence/proposal experiments below Core and human approval; extractor and renderer current-state inventories follow executable evidence; proposed extractor ADR-0006 is removed; renderer ADR-0011 supersedes only ADR-0008 authentication detail. |
| DOC-CON-041–046 | EVA material is reference evidence, historical external-party roles stay temporal, confidence display remains open, workspaces are source-only, nonexistent Codex hooks are removed, and renderer signatures remain separate from Web decorative imagery. |
| DOC-CON-047–051 | Planned design divergences remain explicit; exact amber/navy target values stay open; absent design checkouts are provenance only; Core decisions map distinctly to operator labels/persistence; the nonexistent jquery-validation licence path is removed from the census without claiming an artifact deletion. |

Pull request 18 remains an in-review consolidation from baseline
`467284f23b268e199d7fbe77dbb2163b50f00e23`. Its earlier exact-census,
material-claim, and callsite assertions are withdrawn because their baseline
was unsound. Green structural checks remain link/build evidence only; they do
not establish repository-policy proof, semantic preservation, or acceptance.

### Pull request 18 review remediation

Independent review of head `7f9f088150ff04d8336a38a27e25804dac412d8a`
and its imported-source addendum found 39 required findings. This remediation:

- restores removed unique evidence and the still-live planning dependency
  routes that the consolidation had erased;
- restores externally imported `SKILL.md`, reference, UI, agent, and `dev-ref/`
  material to baseline bytes and adds a repository rule protecting that source
  boundary;
- preserves the user-confirmed Received/Sent/Reply taxonomy and gives settled
  source identity, Triage, lifecycle, access, custody, report, and recovery
  behavior one canonical Core-owned requirements clause each;
- keeps provider research URLs, dated benchmark provenance, the report-renderer
  template contract, and document-extraction predecessor history reachable
  without presenting them as current callers or accepted product behavior;
- corrects the false current caller/deployment claims: the only observed Pegasus
  mutation route is the Development-only intake POST; workspaces remain
  non-callers; and production, browser, hosted recovery, and operator acceptance
  remain unproved;
- supersedes Box File Request with the bounded in-house request-scoped upload
  contract while retaining Box as intended accepted-case custody; and
- updates capability and design links to the canonical clauses without changing
  capability IDs, horizons, or release allocation.

`scripts/Test-RepositoryPolicy.ps1` is temporarily disabled and deferred until
after `0.1.0-alpha.1`. Its direct invocation and its
`scripts/Test-RepositoryLanguage.ps1` caller are successful no-ops, so their
result is **skipped/deferred**, not **passed**, proves no repository-policy
property, and cannot be cited as green evidence. Repository policy is excluded
from the alpha-required gates; other independently operating language, build,
and test gates remain unchanged. Post-alpha activation requires a reviewed
re-enable change, reproducible proof inputs, a clean-checkout pass, and
independent review.

Fresh exact-head checks and independent review are still required. This record
does not infer acceptance from implementation or green checks.

### Single-context domain-documentation cutover

The user accepted the atomic migration plan and authorized its publication on
[issue 6](https://github.com/collisionengineers/pegasus/issues/6#issuecomment-5118943180)
on 2026-07-29. The cutover:

- adds root `CONTEXT.md` as a glossary only;
- moves the root durable-decision authority from `docs/decisions/` to
  `docs/adr/`;
- retains every other canonical source role and every workspace-local decision
  store;
- updates root-authority navigation, policy paths, historical live links, and
  agent-skill consumers atomically; and
- changes no capability allocation, runtime seam, caller, deployment unit,
  provider integration, business rule, or external system.

`DOC-CON-012` records the implementation conflict discovered during the move.
Existing published ADR bodies contained relative links to `ADR-000N-*`
filenames, while the accepted target required standard `000N-*` filenames and
no compatibility aliases. The user explicitly selected editing those immutable
bodies over retaining legacy filenames or creating aliases. The authorized body
edits are limited to relative link destinations; decision clauses, rationale,
status, and provenance remain unchanged.

Decision `0014` records the hard-to-reverse path choice. The unaccepted QDOS
implementation-contract proposal advances from `0014` to `0015` without changing
its proposed product clauses.

The user subsequently classified root workflow ADRs 0007, 0008, 0010, and 0012
as documentation bloat and explicitly authorized their removal. Their workflow
migration evidence remains in
`docs/changes/2026-07-27-azure-workflow-onboarding.md`; active repository policy
remains in `AGENTS.md` and the installed `.agents/skills/` workflows. No application, test, CI, or
runtime consumer depended on those ADRs.

## Verification and evidence

- [x] Canonical product documents and the capability inventory agree with every
  crosswalk row.
- [x] Capability inventory contains 229 unique IDs: Now 128, Next 32, Later 40,
  Not planned 29.
- [x] EVA examples retain the exact ordered 13 keys and all 12 Engineer screens
  have reviewed findings.
- [x] The Pegasus source directory and duplicate EVA source directory are absent.
- [x] Semantic-version, solution-boundary and provider-package checks pass;
  this claim excludes repository policy and its deferred language wrapper.
- [x] Main solution and each imported workspace pass their documented independent
  build/test/smoke route.
- [x] Development-only Web smoke proves the Pegasus UI and health endpoints;
  no non-Development intake route is introduced.
- [ ] Exact-head independent review has no unresolved blocker or required finding.

Independent reviews of head `7f9f088150ff04d8336a38a27e25804dac412d8a`
([consolidated review](https://github.com/collisionengineers/pegasus/pull/18#pullrequestreview-4805816332) and [imported-skill addendum](https://github.com/collisionengineers/pegasus/pull/18#pullrequestreview-4805841502))
found required semantic-preservation, protected-source, executable-contract, and proof-reproducibility defects. The documentation head is not safe as the capability-allocation predecessor until remediation, fresh exact-head evidence, and independent review close every required finding.

## Outcome

Implementation and local caller evidence from the orientation series remain
complete. Pull request 7 retains the source review at
`493189012afee158793d1f5d1602b5708b33e530`; pull request 17 is the completed
integration delivery; pull request 18 is the documentation-centralization
prerequisite under exact-head checks and independent review.
