# FRD-11: Reports, correspondence, and reviewed proposals
> Owner capabilities: RPT, AI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Reports, correspondence, and reviewed proposals

Reports are produced from accepted case facts and source-labelled evidence
through the integrated renderer boundary defined by ADR-0025 and ADR-0028.
The current source owner is Infrastructure; the retired imports are not callers.

### Assessment-report outcomes

Assessment rendering (RPT-02) has one closed outcome vocabulary:
`total_loss`, `repairable`, `cash_in_lieu`, and `contract_repair`. Contract
repair is a distinct fourth outcome; it is not a presentation alias for
repairable. Every outcome uses the same assessment bundle: outcome and
findings, vehicle data and the repair-cost calculation, the itemised repair
specification, the marked damage diagram (D39,
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#damage-record)),
selected vehicle images, the statement and the sign-off Engineer tuple (D31),
and the fee note.

| Outcome | Title and badge | Headline figures | Settlement meaning |
| --- | --- | --- | --- |
| `total_loss` | `TOTAL LOSS REPORT`; `TOTAL LOSS — CATEGORY x` | Pre-accident value, repair cost including VAT, salvage value, and recommended settlement | Recommended settlement is the accepted Engineer value less the accepted salvage value; the accepted category and its approved salvage treatment are required. |
| `repairable` | `REPAIRABLE REPORT`; `REPAIRABLE` | Pre-accident value, labour hours, and repair cost including VAT | Recommended settlement is the calculated repair cost for the Engineer's repairable finding. |
| `cash_in_lieu` | `CASH IN LIEU REPORT`; `CASH IN LIEU` | Pre-accident value, labour hours, and cash-in-lieu settlement | The recommended cash-in-lieu settlement is the calculated repair cost. |
| `contract_repair` | `CONTRACT REPAIR REPORT`; `CONTRACT REPAIR` | Pre-accident value, labour hours, and repair cost including VAT | The Core-computed VAT-inclusive repair total is the agreed contract-repair cap and cannot increase. |

`Pegasus.Core` selects the outcome from the accepted Engineer finding and
owns the calculation of each derived figure once from accepted, source-labelled
inputs. A caller or renderer cannot select an outcome, provide a precomposed
settlement in place of those inputs, or reinterpret one outcome as another.
Missing, unknown, conflicting, or incomplete outcome data fails closed before
an accepted report artifact is rendered. Outcome-specific data is required
where it affects the document, including category and salvage for total loss
and the accepted raw cost components from which Core computes the contract-repair
cap.

The operator has already supplied the report and correspondence templates.
Use those retained assets; there is no outstanding request to invent or supply
template wording. Supplied template, schema, wording, design, and sample material is evidence for
this contract, not a second policy owner. Any category treatment, recovery or
storage paragraph, statement-of-truth wording, qualification, signature, or
other document wording that has not been accepted remains unavailable; the
renderer must not substitute placeholder or inferred content.

### Audit report parity

Audit and Inspection + Audit are active and in scope. An Audit report uses the
same approved Inspection report contract, template, wording, layout, and
renderer presentation as the equivalent Inspection report. Audit is distinct
only in its accepted workflow provenance and immutable internal reference: the
normal Case/PO remains authoritative, with the existing `a.{Case/PO}` reference
for a repairable Audit or `ap.{Case/PO}` for a total-loss Audit. Those identity
facts travel through the shared Core-owned report contract; they do not select
or create a separate physical report family.

Missing, conflicting, ambiguous, stale, or cross-case Audit outcome or
reference evidence fails closed before rendering. Audit must not introduce a
second template, wording, layout, report model, conservative/maximised
specification pair, or monetary or percentage uplift. Inspection and Audit use
the same governed renderer boundary with their own evidence and references.

### Initial renderer activation

The active renderer uses the `rendererref1` assessment and fee note for
Inspection, Audit and Inspection + Audit. Diminution, addendum,
valuation-evidence and generic-letter families are not activated by this contract;
there is no caller-selectable template or density setting. Core accepts an
immutable, source-labelled snapshot, validates readiness and the supplied
sign-off tuple, computes the figures once, and selects one of the four
outcomes. Infrastructure renders only that selection with the governed
template, stylesheet, logo, and supplied signature image.

The report snapshot receives the Case's sign-off tuple — printed name,
qualifications and signature image — read from the Sign-off Engineer account
setting (D31, 2026-09-02;
[FRD-01](frd-01-case-identity-and-lifecycle.md#sign-off-engineer),
[FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)). D31
supersedes D18: typed Engineer identity alone is no longer the rendered
signatory. The printed name and signature image are required; qualifications
are optional, and a report with none prints the name alone. Selection of the
Case sign-off Engineer is governed by FRD-01, while the account tuple and its
eligibility are governed by FRD-04. Missing or unsupported required signatory
content, source version, custody reference or required value fails closed. No custom
signature path, arbitrary local attachment path, placeholder, or wording absent
from the accepted evidence is permitted. The sign-off tuple on every report is
allocated to `DOCS-017`.

Generation remains deterministic, versioned, retained and review-gated, and
generation, approval, issue, sending, external receipt and Case closure remain
distinct recorded events.

Generation returns draft assessment and fee-note artifacts with their bytes,
hashes, page counts, template version, and engine version. It is not approval,
issue, sending, external receipt, durable report-reference allocation, or
correction custody. Human approval remains required before issue; the durable
trigger, immutable reference/version and custody workflow is separately owned.

A generation freezes an immutable snapshot of the Case version, signatory
account and signature digest, Current estimate identity/version and breakdown,
accepted Engineer value and applied valuation identity, content switches,
report date or override, narrative, fee, source documents with Box identities,
and prepared-image role, order, rotation and crop. Report and fee note are
separately addressable generated artifacts through custody. Relevant accepted
fact changes mark a generation stale; notes and recipient edits do not. A
ready generation records ActionHistory `case_report_generation_ready`.
Preview creates neither an artifact nor Sent evidence.

Snapshot assembly captures the Case version before reading its components and
refuses a changed version before freezing; the resolved signatory tuple is
rechecked in the freeze transaction. Confirming or removing source evidence,
or changing the effective signatory's eligibility, name, qualifications or
signature, invalidates affected current generations in the same transaction.
Report outputs identified by their generated-artifact operation identity are
not report inputs and do not invalidate their own generation. Other retained
artifacts remain source evidence regardless of their transport's source label.
Stale generations cannot be prepared or sent; notes and recipient edits alone
do not require regeneration. A delivery preparation still requires current
addressing. Default report dates and displayed report times use Europe/London.

### Report generation entry point

The Report section of the Case record exposes **Generate report** to the
authorized staff roles under the existing state, version and lease gates.
It uses the accepted saved facts and the immutable snapshot defined above.
Generation retains versioned report/fee-note artifacts, their custody outcome
and generation history. A generated artifact is not approval, sending or receipt.

A fee-note preview is non-persisting presentation of the recorded fee and
description. It does not replace retained report generation or create a new
report family. Native Hand to Engineer opens engineering work without an EVA
export; EVA is optional and does not gate report readiness.

Settlement and Report editors use the Case's one workspace Save and share its
reason, expected version, and edit lease. Report records Engineer comments,
agreed fee, description lines, an eligible Sign-off Engineer, the existing
content switches, and report-date override. Vehicle History is edited once in
Vehicle. Unsubmitted accepted values remain unchanged; explicit clears and
false values are submitted values. Switching off the date override does not
clear an unsubmitted recorded date. Validation and concurrency refusals retain
bounded current-versus-proposed values.

Engineer sections remain viewable in other states, with edits governed by
FRD-01. Report readiness adds only genuine post-Review requirements: required
sign-off content and accepted estimate figures. It does not ask staff to
reconfirm unchanged requirements already satisfied for Review. Unexpectedly
missing accepted state fails generation rather than inventing values.

### Report correction, finality, and post-report work

**Accepted report boundary:** an issued report has an immutable artifact/version identity and hash. A
correction or addendum creates a new reasoned version and retains every earlier
artifact, accepted fact, actor, time, and source; it never silently overwrites
the issued report. Further report changes follow FRD-01
and retain the previous artifact and its correction history.

The report-sent business event is the exact approved-mailbox Sent-item evidence
specified in [FRD-08 § Outbound correspondence evidence](frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)
and remains final if Outlook later moves or deletes the item.
Outlook `sentDateTime` remains the business time; discovery and link times are
not substitutes. Report sent enters post-report work rather than closing the
case. A Box report PDF, file upload, generated artifact, draft, queue result, or
staff assertion alone proves neither sending nor external receipt.

Post-report queries, disputes, amendment requests, and replies remain
case-owned correspondence with source/reply-chain identity and permanent
history. The Engineer responds to them. FRD-01 owns the reversible
Completed → Query → Completed cycle: query receipt or attachment enters Query,
and replying completes that work. Further engineering changes use a reasoned
return to engineering and normal edit authority. A mailbox adapter invokes the
shared transition; it does not allocate a new Case/reference.

Requirements:

- deterministic template and payload versioning;
- preserved document/source provenance;
- authorised human review and approval of report facts and content before
  issue, without inventing a separate case-lifecycle pre-send review gate;
- immutable issued artifact identity and hash;
- correction/addendum rather than silent overwrite;
- exact delivery evidence where the workflow requires it;
- accessible staff presentation of status, validation, and failure without
  implying an unproved external delivery.

### Targeted sending and reviewed AI proposals

An allocated targeted report-send transaction is idempotent and records
approved destinations, immutable artifact/version, Box filing, exact send
evidence, completion outcome, and partial-failure recovery. A correction does
not silently alter an issued fee note or invoice; later financial impact uses
its own versioned, authorised contract. Staff-selected AI Assessor and
Engineer-reviewed query proposals remain proposals until the authorised human
accepts or rejects them through Core.

The vendor-neutral `Send to AI` work transport (AI-09; governed by ADR-0031
under the operator's 2026-08-03 direct-write decision) hands a scoped worker
a pointer to one case — never case content — and the worker returns its work
as ordinary Automation Actor writes through the same Core commands, edit
lease, operation-key replay, and version guards as a staff save, attributed
and permanently recorded with the same rigor as any human action. Values the
automation records are unconfirmed working data reviewed by the engineer the
case is manually assigned to. Confirming a professional finding is
staff-Engineer-only, and report approval and outward dispatch remain human
acts, so no model, skill, prompt, or external source ever issues an accepted
case, engineering, economic, legal, or report outcome.

Durable Send to AI work has stable request, hand-off, reply, and disposition
identities. Stale work cannot overwrite a newer case/evidence version;
duplicate, expired, or cancelled requests are idempotent or inert outcomes of
the tracking record that never mutate accepted data; no AI caller confirms,
approves, or sends autonomously.

### AI Job List

The AI Job List is the AI-10 catalogue: one durable ledger of named AI jobs
([ADR-0035](../adr/0035-ai-job-ledger.md)) that external AI clients claim
through the Automation Actor ([FRD-10 § AI job and estimate
tools](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)).
Pegasus never runs an AI job itself and never applies a job's result to
accepted data; every result is a draft or proposal that a staff act confirms
through the existing action for that record. Visuals follow
`docs/design/README.md`.

**Kinds.** The catalogue is a closed Core list; an unknown kind is refused at
creation.

| Kind | Started from | Input | Result | Staff confirmation |
| --- | --- | --- | --- | --- |
| Estimate | Estimate section `Send to Claude` (With Engineer or onwards) | Direction text and an optional target percentage of the recorded Engineer's Value — 0 to 80 %, no default, its amount shown as it is derived from that value, proposal guidance only and never an accepted figure (D24); refused without an Engineer's Value | A drafted estimate saved on the Case through the estimate tools, citing the job; state `Draft` | An Engineer accepts the draft (`Use estimate`), which makes it the Current estimate |
| Unidentified resolution | Operations `Send Unidentified to AI` for one U reference | The U reference only | A proposed destination (existing Case, new Case from an accepted instruction, Image-initiated Case, or close) and a reason | Staff confirm through the existing Unidentified resolve action; the proposal never resolves the item itself |
| Query response | A retained post-report query linked to a Case | The message reference only | Draft reply text | Offered to the composer or Case notes; never sent automatically |
| Unidentified-queue pass | An external scheduler through the Actor `create` tool — Pegasus runs no timer | The queue scope | One Unidentified-resolution proposal per item the pass examined | As Unidentified resolution, per item |
| MarketResearch | Select Market Research on the Case Valuation screen | The Case and its valuation context; external Claude Cowork uses the Pegasus connector plus research tools outside this repository | Research files attached to the Case through the connector, with attributable evidence and optional source-labelled valuation entries | The Automation Actor marks the job Completed after attachment; no staff completion gate and no automatic adoption as the Engineer's Value |

**States.** Reviewed proposals follow `Queued` → `Taken` → `Draft ready` →
`Completed`. MarketResearch follows `Queued` → `Taken` → `Completed` after
retention of its files. All kinds also have `Failed`,
`Cancelled` and `Expired` as the other terminal states.

- `Queued`: created and claimable. Creation records the kind, the target
  record, and *started by* — a staff username or the connector client name.
- `Taken`: claimed by a named connector client under a lease with a visible
  expiry. A lease that expires returns the job to `Queued` and records the
  expired claim; a client may release a job back to `Queued` before then.
- `Draft ready`: the client has written its result and named it on the job;
  the job waits for staff.
- `Completed`: the staff consumption act has been recorded for a reviewed
  proposal, or staff completed a Query response/Unidentified-queue pass. For
  MarketResearch, the Automation Actor completes the taken job after the
  connector attaches its research files to the Case; it does not wait for a
  staff act or imply acceptance of an Engineer's Value.
- `Failed`: the client reported failure with a reason; the job is not
  re-queued automatically.
- `Cancelled`: staff cancelled with a reason; a taken job is cancelled at
  once and the client's next progress call is refused.
- `Expired`: a job that was never taken before its own expiry.

Every transition carries an operation key and an expected version; a stale
or duplicate transition is an inert, recorded outcome. Transitions by a
client are attributed to the Automation Actor and the client name; staff
transitions to the staff username. The Administrator kill switch refuses
claims and progress; queued jobs wait and taken jobs expire back to `Queued`.

**Operations panel.** The AI Job List on `/operations` shows every non-terminal
job and the terminal jobs of the current day: Job (kind and detail), Record,
Started by, Created, State, Action. The action is one of `Review estimate`
(opens the Case record's Estimate section), `Open query` (opens the message), or
`Review` (opens the Unidentified item) for a `Draft ready` job; `Complete
job` for a `Draft ready` Query response or Unidentified-queue pass; `Cancel`
(reason required) for any non-terminal job; otherwise nothing. `Send
Unidentified to AI` creates an Unidentified-resolution job for a chosen U
reference.

**Administration.** Automation & AI shows the active and failed job counts and
the Stop/Start automation control; that control is the ADR-0026 kill switch,
so stopping automation also stops the ledger.

### Estimate VAT on the rendered report

Each estimate has its own VAT percentage, defaulting to 20, and selected VAT
categories. `Unknown` repairer VAT blocks Use as Current until staff record an
explicit status or explicit categories. For a rendered report, VAT is
`Taxable × VatPercent / 100`, where Taxable is the selected discounted Labour,
Parts, Materials and Specialist categories. `Pegasus.Core` computes each
printed component independently; printed Net is their sum and printed Gross is
printed Net plus printed VAT. No residual penny moves between components.

| Figure | Rule |
| --- | --- |
| Parts | Explicit part prices × quantity |
| Labour | Panel and paint hours × the selected labour-rate-card rate |
| Parts, materials and specialist | Explicit estimate amounts, discounted where selected |
| Taxable | Selected discounted Labour, Parts, Materials and Specialist categories |
| VAT | Taxable × VAT % |
| Net / Gross | Sum of independently rounded printed components / Net + printed VAT |

No comparison figure between an imported provider version and
an assessed version, and no savings figure, is computed or rendered (D17).
Normalized provider and manual estimates are editable records; their retained
raw source evidence and hashes are immutable. Every direct change follows the
same lease, expected-version, attribution, reason, and history contract.

Signatures embedded in governed renderer documents are provenance-sensitive
document assets, not Web decorative imagery. The signatory is the Case's
Sign-off Engineer (D31); rendering that tuple is allocated to `DOCS-017`.
