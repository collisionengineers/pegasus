# FRD-11: Reports, correspondence, and reviewed proposals

> Owner capabilities: RPT, AI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Core picks one of four report outcomes from the Engineer's accepted
  finding and computes every figure once. A renderer never chooses or
  reinterprets an outcome.
- An Audit report uses the same template and layout as an Inspection report.
  Only its reference and provenance differ.
- Generating a report is not approving, sending or receiving it. Each is a
  separate recorded event.
- An issued report is immutable. A correction is a new version; the old one
  is kept.
- AI never decides anything. Its output is a proposal until a staff member
  accepts or rejects it.

## Purpose

This document owns how reports are produced, corrected and sent, and how AI
proposals are reviewed. Reports come from accepted Case facts and
source-labelled evidence through the integrated renderer
([ADR-0025](../adr/0025-integrate-renderer-and-extractor-into-the-application.md),
[ADR-0050](../adr/0050-questpdf-report-renderer.md)). Infrastructure owns
the renderer source; the retired imports are not callers.

## Behaviour

## Reports, correspondence, and reviewed proposals

### Assessment-report outcomes

There are exactly four outcomes: `total_loss`, `repairable`, `cash_in_lieu`
and `contract_repair`. Contract repair is its own outcome, not a variant of
repairable. Every outcome uses the same bundle: outcome and findings, vehicle
data and the repair-cost calculation, the itemised repair specification, the
marked damage diagram
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#damage-record)),
selected vehicle images, the statement, the sign-off Engineer tuple, and the
fee note.

| Outcome | Title and badge | Headline figures | Settlement meaning |
| --- | --- | --- | --- |
| `total_loss` | `TOTAL LOSS REPORT`; `TOTAL LOSS — CATEGORY x` | Pre-accident value, repair cost including VAT, salvage value, recommended settlement | Recommended settlement is the accepted Engineer value less the accepted salvage value. The accepted category and its approved salvage treatment are required. |
| `repairable` | `REPAIRABLE REPORT`; `REPAIRABLE` | Pre-accident value, labour hours, repair cost including VAT | Recommended settlement is the calculated repair cost for the Engineer's repairable finding. |
| `cash_in_lieu` | `CASH IN LIEU REPORT`; `CASH IN LIEU` | Pre-accident value, labour hours, cash-in-lieu settlement | The recommended cash-in-lieu settlement is the calculated repair cost. |
| `contract_repair` | `CONTRACT REPAIR REPORT`; `CONTRACT REPAIR` | Pre-accident value, labour hours, repair cost including VAT | The Core-computed VAT-inclusive repair total is the agreed contract-repair cap and cannot increase. |

Core selects the outcome from the accepted Engineer finding and computes
each figure once from accepted, source-labelled inputs. A caller or renderer
cannot pick an outcome, supply a ready-made settlement, or read one outcome
as another. Missing, unknown, conflicting or incomplete outcome data fails
before any report is rendered. Data that affects the document is required:
category and salvage for total loss, and the raw cost components that Core
uses for the contract-repair cap.

The operator has supplied the report and correspondence templates. Pegasus
uses those. Supplied templates, schemas, wording, designs and samples are
evidence for this contract, not a second rule owner. Any wording that has not
been accepted, such as a category treatment, storage paragraph,
statement-of-truth text, qualification or signature, is unavailable. The
renderer never fills a gap with a placeholder or a guess.

### Audit report parity

Audit and Inspection + Audit are active. An Audit report uses the same
approved contract, template, wording, layout and presentation as the
matching Inspection report. What differs is the Case's provenance and its
reference:

- A standalone Audit Case's Case/PO is the `a.` reference itself, for
  example `a.QDOS26002`.
- For Inspection + Audit, the Inspection Case keeps `QDOS26001` and its
  linked Audit Case is `a.QDOS26001`.

The assessment outcome never changes the reference. The assessment is a
separate Case fact
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
Those identity facts travel through the shared Core report contract; they do
not create a separate report family.

For Inspection + Audit, the Inspection report is produced on the Inspection
Case and the Audit report on the linked Audit Case that Create audit makes.
Each Case generates, approves and sends its own report under its own
reference. The Audit report never overwrites or reissues the Inspection
report.

Audit outcome or reference evidence that is missing, conflicting, ambiguous,
stale or from another Case fails before rendering. Audit adds no second
template, wording, layout, report model, conservative/maximised pair, or
money or percentage uplift.

### Initial renderer activation

The active renderer uses the `rendererref1` assessment and fee note for
Inspection, Audit and Inspection + Audit. Diminution, addendum,
valuation-evidence and generic-letter families are not activated. There is
no caller-selectable template or density. Core takes an immutable,
source-labelled snapshot, checks readiness and the sign-off tuple, computes
the figures once, and selects one outcome. Infrastructure renders that
selection with the governed layout, embedded fonts, logo and supplied
signature image.

**Sign-off on the report.** The snapshot carries the Case's sign-off tuple:
printed name, qualifications and signature image, read from the Sign-off
Engineer account. The name and signature image are required. Qualifications
are optional; without them the name prints alone. Who is offered as Sign-off
Engineer and the default are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#sign-off-engineer). The
account flag and its data are in
[FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts). Typed
Engineer identity alone is never the signatory. Missing or unsupported
signatory content, source version, custody reference or required value
fails. No custom signature path, local attachment path, placeholder or
unaccepted wording is allowed.

**Generation is one step of several.** Generation is deterministic,
versioned, retained and gated on readiness. Generation, approval, issue,
sending, external receipt and Case closure are separate recorded events.
Generation returns draft assessment and fee-note files with bytes, hashes,
page counts, template version and engine version. It is not approval, issue,
sending, receipt, reference allocation or correction custody. A human must
approve before issue.

**What a generation freezes.** The Case version, signatory account and
signature digest, Current estimate identity, version and breakdown, accepted
Engineer value and applied valuation identity, content switches, report date
or override, narrative, fee, source documents with their Box identities, and
each prepared image's role, order, rotation and crop.

**Report and fee note.** They are separately addressable files in custody.
The operator generating the report chooses whether the fee note is a separate
document or the report's final pages. The snapshot records that choice, so a
combined report is one file under the report's name and reproduces the same
way. A later request for a separate fee note names the current confirmed,
non-stale generation and adds the fee note from that generation's frozen
date and fee facts. It does not re-freeze the report. The request is refused
when there is no current generation, the generation is stale, or the report
already contains its fee note. Fee facts, readiness and accepted fee terms
are the same either way.

**Staleness.** One Core rule over normalised effective values marks a
generation stale only when an accepted report fact changes. Notes, no-op
saves and recipient edits do not. A ready generation records
`case_report_generation_ready` in history. Stale generations cannot be
prepared or sent.

**Views and downloads.** A preview creates no file and no Sent evidence.
Viewing one records `case_report_draft_previewed`, distinct from generation
and download. Reopening a confirmed file's bytes records
`case_report_artifact_downloaded`, at most once per Case or file, staff
member and London day.

**Version checks.** Snapshot assembly reads the Case version first and
refuses a changed version before freezing. Generation and preparation
commands carry the version shown in the browser and refuse a stale one
rather than re-reading. An operation key replays only the same file kind,
packaging choice and target generation; reusing it for a different command
is a conflict. The signatory tuple is rechecked in the freeze transaction.
Confirming or removing source evidence, or changing the signatory's
eligibility, name, qualifications or signature, invalidates affected current
generations in the same transaction. Report outputs are not report inputs
and do not invalidate their own generation. Other retained files stay source
evidence whatever their transport label.

**Recipients.** A delivery preparation needs current addressing. Principal
recipient settings can include the original instruction sender and any
number of extra addresses. The original sender comes from the originating
instruction, never the latest reply; an unresolved sender adds no invented
address. Claim Source is never copied implicitly. Recipients are frozen in
the preparation, but the Engineer may edit To and Cc before that freeze.
Every delivery still needs a staff-controlled send. Default report dates and
displayed times use Europe/London.

### Report generation entry point

The Report section of the Case record offers **Generate report** to
authorised staff roles under the existing state, version and lease gates. It
uses the accepted saved facts and the snapshot above, and retains versioned
report and fee-note files, their custody outcome and history. A generated
file is not approval, sending or receipt.

A fee-note preview shows the recorded fee and description without saving
anything. Native Hand to Engineer opens engineering work without an EVA
export; EVA is optional and never gates report readiness.

The Estimate section offers **Estimate PDF**, an unretained Estimate
document in the house style for any saved estimate version from
`EstimateTotals`. Viewing it records `case_estimate_document_previewed`. It
is not a report, approval, delivery or correspondence.

Settlement and Report editors use the Case's one workspace Save with its
version and edit lease
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)); a save needs no
reason. Report records Engineer comments, agreed fee, description lines, an
eligible Sign-off Engineer, the content switches and the report-date
override. Beside the valuation commentary switch it records **valuation
commentary** text, up to 4,000 characters. With the switch on, the report
prints that text if recorded, otherwise the applied valuation's reason, and
readiness accepts either. With the switch off, neither prints. Vehicle
History is edited once, in Vehicle. Values not submitted stay unchanged;
explicit clears and false values count as submitted. Turning off the date
override does not clear an unsubmitted recorded date. Validation and
concurrency refusals keep the current and proposed values for comparison.

Engineer sections stay viewable in other states; edits follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions). Report readiness
adds only real post-Review requirements: the sign-off content and accepted
estimate figures. It never asks staff to reconfirm what Review already
checked. Missing accepted state fails generation rather than inventing
values.

### Report correction, finality, and post-report work

An issued report has an immutable file and version identity and hash. A
correction or addendum creates a new reasoned version and keeps every
earlier file, fact, actor, time and source. It never silently overwrites the
issued report. Later report changes follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions) and keep the
correction history.

Report sent is the exact approved-mailbox Sent-item evidence in
[FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence).
Report sent starts post-report work; it does not close the Case.

Post-report queries, disputes, amendment requests and replies stay Case
correspondence with source and reply-chain identity and permanent history.
The Engineer answers them. The Completed → Query → Completed cycle is in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query). A
mailbox adapter calls that shared transition; it never allocates a Case or a
reference.

Requirements:

- deterministic template and payload versioning;
- preserved document and source provenance;
- authorised human review and approval of facts and content before issue,
  with no separate pre-send review gate in the Case lifecycle;
- immutable issued file identity and hash;
- correction or addendum, never silent overwrite;
- exact delivery evidence where the workflow requires it;
- accessible status, validation and failure display that never implies an
  unproved delivery.

### Targeted sending and reviewed AI proposals

A targeted report send is idempotent. It records approved destinations, the
immutable file and version, Box filing, exact send evidence, completion
outcome and partial-failure recovery. A correction never silently alters an
issued fee note or invoice; later financial impact uses its own versioned,
authorised contract. AI Assessor and Engineer-reviewed query proposals stay
proposals until an authorised person accepts or rejects them through Core.

**Send to AI.** The vendor-neutral `Send to AI` transport
([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md))
hands a scoped worker a pointer to one Case, never Case content. The worker
writes back through the same Core commands, edit lease, operation-key replay
and version guards as a staff save, attributed and recorded like any human
action. What the automation records is unconfirmed working data for the
assigned Engineer to review. Confirming a professional finding is for staff
with Engineer capabilities, including Administrator. Report approval and
sending stay human acts. No model, skill, prompt or external source ever
issues an accepted Case, engineering, financial, legal or report outcome.

**Settlement proposals.** For Outcome, Engineer's Value, Salvage category,
Salvage value, Roadworthiness and the unroadworthy reason, Pegasus keeps the
latest Automation value as a proposal with a status. It reads **Awaiting**
until staff record the field. A matching staff value makes it **Accepted**; a
different value or a clear makes it **Corrected**, recording who resolved it.
It stays resolved until the next Automation value. The Settlement section
shows the Proposed column only when a proposal exists. It offers Accept and
Accept all only while a proposal is Awaiting and the Case is being edited.
The Engineer's Value row offers Apply in Valuation instead, because that
value is adopted only through an explicit valuation Apply. A Save that leaves
an Awaiting field untouched leaves it Awaiting.

Durable Send to AI work has stable request, hand-off, reply and disposition
identities. Stale work cannot overwrite a newer Case or evidence version.
Duplicate, expired or cancelled requests are inert, recorded outcomes that
never change accepted data. No AI caller confirms, approves or sends on its
own.

### AI Job List

The AI Job List is one durable ledger of named AI jobs
([ADR-0035](../adr/0035-ai-job-ledger.md)) that external AI clients claim
through the Automation Actor
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)).
Pegasus never runs an AI job itself and never applies a result to accepted
data. Every result is a draft or proposal that a staff act confirms through
the normal action for that record. Visuals follow
[design](../design/README.md).

**Kinds.** A closed Core list. An unknown kind is refused at creation.

| Kind | Started from | Input | Result | Staff confirmation |
| --- | --- | --- | --- | --- |
| Estimate | Estimate section `Send to AI` (With Engineer or later) | Direction text and an optional target percentage of the recorded Engineer's Value, 0 to 80 %, no default; the amount is shown as derived from that value and is guidance only, never an accepted figure. Refused without an Engineer's Value | A drafted estimate saved on the Case through the estimate tools, citing the job; state `Draft` | An Engineer accepts the draft (`Use estimate`), which makes it the Current estimate |
| Unidentified resolution | Operations `Send Unidentified to AI` for one U reference | The U reference only | A proposed destination (existing Case, new Case from an accepted instruction, Image-initiated Case, or close) and a reason | Staff confirm through the existing Unidentified resolve action; the proposal never resolves the item itself |
| Query response | A retained post-report query linked to a Case | The message reference only | Draft reply text | Offered to the composer or Case notes; never sent automatically |
| Unidentified-queue pass | An external scheduler through the Actor `create` tool; Pegasus runs no timer | The queue scope | One Unidentified-resolution proposal per item examined | As Unidentified resolution, per item |
| MarketResearch | **AI market research** in the Case record's Valuation section, while editing, for the chosen Valuation month. The section shows a "Researching · {month}" card while the job is Queued or Taken; a re-run replaces the card | The Case and its valuation context. External Claude Cowork uses the Pegasus connector plus research tools outside this repository | Research files attached to the Case through the connector, with attributable evidence and optional source-labelled valuation entries | The Automation Actor marks the job Completed after attachment. No staff completion gate and no automatic adoption as the Engineer's Value |

**States.** Reviewed proposals go `Queued` → `Taken` → `Draft ready` →
`Completed`. MarketResearch goes `Queued` → `Taken` → `Completed` once its
files are retained. All kinds can also end in `Failed`, `Cancelled` or
`Expired`.

- `Queued`: created and claimable. Creation records the kind, the target
  record and *started by* (a staff username or the connector client name).
- `Taken`: claimed by a named connector client under a lease with a visible
  expiry. An expired lease returns the job to `Queued` and records the
  expired claim. A client may release a job back to `Queued` before then.
- `Draft ready`: the client has written and named its result; the job waits
  for staff.
- `Completed`: staff recorded the consumption act for a reviewed proposal,
  or completed a Query response or Unidentified-queue pass. For
  MarketResearch, the Automation Actor completes the taken job after the
  connector attaches its files; no staff act and no acceptance of a value is
  implied.
- `Failed`: the client reported failure with a reason; not re-queued
  automatically.
- `Cancelled`: staff cancelled with a reason. A taken job is cancelled at
  once and the client's next progress call is refused.
- `Expired`: never taken before its own expiry.

Every transition carries an operation key and an expected version. A stale
or duplicate transition is an inert, recorded outcome. Client transitions
are attributed to the Automation Actor and the client name; staff
transitions to the staff username. The Administrator kill switch refuses
claims and progress; queued jobs wait and taken jobs expire back to `Queued`.

**Operations panel.** The AI Job List on `/operations` shows every
non-terminal job and the terminal jobs of the current day: Job (kind and
detail), Record, Started by, Created, State, Action. The action is one of
`Review estimate` (opens the Estimate section), `Open query` (opens the
message) or `Review` (opens the Unidentified item) for a `Draft ready` job;
`Complete job` for a `Draft ready` Query response or Unidentified-queue
pass; `Cancel` (reason required) for any non-terminal job; otherwise
nothing. `Send Unidentified to AI` creates an Unidentified-resolution job for
a chosen U reference.

**Administration.** Automation & AI shows the active and failed job counts
and the Stop/Start automation control. That control is the
[ADR-0026](../adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md) kill switch, so stopping
automation also stops the ledger. The Operations panel is the live queue;
the history of the same jobs is
[Action logs](frd-04-parties-accounts-and-access.md#permanent-action-history),
where an AI job row's Reference opens the Case or Unidentified record.

### Estimate VAT on the rendered report

Each estimate has its own VAT percentage, default 20, and selected VAT
categories. `Unknown` repairer VAT blocks Use as Current until staff record
an explicit status or explicit categories. On a rendered report, VAT is
`Taxable × VatPercent / 100`, where Taxable is the selected discounted
Labour, Parts, Materials and Specialist categories. Core computes each
printed component on its own. Printed Net is their sum; printed Gross is
printed Net plus printed VAT. No residual penny moves between components.

| Figure | Rule |
| --- | --- |
| Parts | Explicit part prices × quantity |
| Labour | Panel, paint and Specialist work-unit hours × the selected labour-rate-card rate; hours on a fixed-price Specialist line are kept, shown and not priced |
| Parts, materials and specialist | Explicit estimate amounts, discounted where selected |
| Taxable | Selected discounted Labour, Parts, Materials and Specialist categories |
| VAT | Taxable × VAT % |
| Net / Gross | Sum of independently rounded printed components / Net + printed VAT |

No comparison between an imported provider version and an assessed version,
and no savings figure, is computed or shown. Normalised provider and manual
estimates are editable records; their raw source evidence and hashes are
immutable. Every change follows the same lease, version, attribution, reason
and history rules.

Signatures in rendered documents are provenance-sensitive document assets,
not decorative images. The signatory is the Case's Sign-off Engineer.

## States and transitions

A report generation is `ready`, then `stale` when an accepted fact changes.
An issued report is immutable; a correction is a new version. AI job states
are listed above. The Case's own states are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- Any missing, unknown or conflicting outcome data stops rendering.
- A stale version in the browser is refused, never replaced.
- A stale generation cannot be prepared or sent.
- A fee-note request against a report that already contains one is refused.
- An unknown AI job kind is refused; stale or duplicate transitions are
  inert.
- No generated file, preview, draft or export counts as Report sent.

## Acceptance evidence

Core tests cover outcome selection, the figure calculations and VAT table,
snapshot freezing and staleness, the fee-note rules, and every AI job
transition. Integration tests cover Generate report, previews and downloads
with their history events, and the Operations panel actions. Rendering
against the supplied templates is verified by retained sample output.
Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `RPT-*`, `AI-*` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0025](../adr/0025-integrate-renderer-and-extractor-into-the-application.md),
  [ADR-0026](../adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0035](../adr/0035-ai-job-ledger.md),
  [ADR-0050](../adr/0050-questpdf-report-renderer.md).
