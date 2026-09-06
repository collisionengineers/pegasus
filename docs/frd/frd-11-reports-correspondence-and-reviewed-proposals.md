# FRD-11: Reports, correspondence, and reviewed proposals
> Owner capabilities: RPT, AI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Reports, correspondence, and reviewed proposals

Reports are produced from accepted case facts and source-labelled evidence
through the approved renderer boundary. Renderer source workspaces remain
independent source imports until an accepted integration contract and real
application caller exist.

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

Supplied template, schema, wording, design, and sample material is evidence for
this contract, not a second policy owner. Any category treatment, recovery or
storage paragraph, statement-of-truth wording, qualification, signature, or
other document wording that has not been accepted remains unavailable; the
renderer must not substitute placeholder or inferred content.

### Audit report parity

When RPT-03 is activated by its own accepted caller, an Audit report uses the
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
specification pair, or monetary or percentage uplift. This future behaviour
does not open the current renderer surface or supply a caller; the closed
activation boundary below remains in force.

### Initial renderer activation

The first active renderer surface is closed to the `rendererref1` assessment
and its fee note. Audit, diminution, addendum, valuation-evidence, generic
letter, and every other former workspace catalogue family are unavailable;
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

### Report-draft entry point

The renderer boundary above is reachable from one operator action (DELIV-012):
a "Generate report draft" control on the Report section of the Case record
(`/Cases/{id}?section=report`; `/Cases/{id}/Assessment` is a permanent
redirect, D30), open to the same staff roles as the rest of that
record (Administrator, Engineer, User). It projects the case's already-saved,
confirmed assessment record into the accepted snapshot, renders it, and
returns the assessment PDF to the operator's browser. Nothing is saved,
approved, or sent by this action — it is strictly the draft-generation step
the renderer boundary above already defines; approval and issue remain the
separately owned human acts described below.

The Engineer sections of the Case record — Damage, Valuation, Estimate,
Settlement, Report — are always viewable (D30, 2026-09-02). They are editable
in `Report preparation` and `Post report` (displayed "With Engineer") under
the Case edit lease, and read-only in `Post-report complete` and the other
terminal outcomes; the former D11 access rule is now this read-only rule.
Report generation does not depend on an EVA export or submission. EVA is
optional and never gates report readiness; its hand-off and report preparation
remain separate workflows.

**Fee note preview.** The Report section renders a fee note preview from the
agreed fee and the description lines recorded on the Case (D42, 2026-09-02).
It is a preview of the fee-note artifact the renderer emits; sending stays
`MAIL-17`.

**Readiness.** A single readiness rail decides whether the control is enabled:
`AssessmentPolicy.EvaluatePostReviewReadiness` (the Assessment screen's
post-Review list) plus only requirements first introduced after the case
entered `Review`: the Case's sign-off Engineer tuple and the accepted estimate
figures (below). Requirements already enforced by the transition into `Review` are not
recalculated as report readiness. The saved case identity, instruction,
inspection and custody facts are consumed when the draft is generated; if one
is unexpectedly absent, generation fails as an invalid case state rather than
presenting the operator with a duplicate readiness task. A case missing a true
post-Review requirement leaves the control disabled and states that outstanding
reason by name; nothing is guessed to make the control available.

**Photographs and source evidence.** `Photos` are the case's custody-confirmed
`Image`-role documents (current, not logically removed, custody status
Confirmed) — the same confirmation gate the EVA hand-off bundle already uses
for its own image evidence. `Sources` are every other custody-confirmed case
document, reported by its own file name, version and hash. Both are real
custody facts: every confirmed image on the case is offered to curation, and
nothing is filtered out before the Engineer sees it. Their absence after entry
to `Review` is an invalid case state, not another report-readiness
classification.

Curation itself is decided (D19, 2026-09-01) and no longer deferred with the
rest of the UI-15 workbench: preparation is non-destructive, a report requires
distinct `Close-up` and `Overview` images in that order, optional supporting
images follow in explicit operator order, crop and ordering data are
normalized, versioned, attributable and protected by expected-version and
edit-lease rules, and an issued report retains its exact curation snapshot and
source hashes
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis)).
Allocated to [[ENG-031]]; not delivered.

**Repair-cost figures.** One labour-rate-card snapshot prices both panel and
paint hours. Parts, materials and specialist costs remain explicit estimate
amounts. The selected estimate's VAT categories and its own VAT percentage
determine the taxable base.
Multiple global, versioned labour-rate cards exist as Administrator-managed
configuration (id, name, panel-and-paint hourly rate, enabled state, actor,
timestamps); staff select one card for every new or amended estimate version,
and a report version records the card version it used. Disabling a card blocks
future selection without changing history. Normalized imported and manual
estimates are directly editable under the ordinary expected-version and
edit-lease rules. Their retained source artifacts and hashes remain immutable;
every change records actor, time, reason and before/after values, and may move
the estimate through Draft, Accepted and Current. No original-versus-assessed
comparison figure and no savings figure exists, in the editor or on the
report. A case whose current estimate version has no selected card names that
card as the outstanding readiness reason; nothing is fabricated. The rate-card
aggregate itself is allocated to `TICK-082` and is not yet delivered.

### Report correction, finality, and post-report work

**Accepted report boundary:** an issued report has an immutable artifact/version identity and hash. A
correction or addendum creates a new reasoned version and retains every earlier
artifact, accepted fact, actor, time, and source; it never silently overwrites
the issued report. A closed case must be reasonedly reopened before its report
or evidence is revised.

The report-sent business event is the exact approved-mailbox Sent-item evidence
specified in [FRD-08 § Outbound correspondence evidence](frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)
and remains final if Outlook later moves or deletes the item.
Outlook `sentDateTime` remains the business time; discovery and link times are
not substitutes. Report sent enters post-report work rather than closing the
case. A Box report PDF, file upload, generated artifact, draft, queue result, or
staff assertion alone proves neither sending nor external receipt.

Post-report queries, disputes, amendment requests, and replies remain
case-owned correspondence with source/reply-chain identity and permanent
history. Collision Engineers' Engineer responds to them, but the exact
CASE-23 states, transitions, correction/reopen interaction, due/chaser
interaction, and closure rules remain `Next`/unallocated and unresolved; no
mailbox adapter may invent them or create a new case/reference. See [external
data, submission, and report
contracts](../open-decisions.md#external-data-submission-and-report-contracts).

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
| MarketResearch | The Case record's Valuation section (D35) | The Case. The research runs outside Pegasus: the operator's Claude Cowork connector polls the job ledger through the Automation Actor, searches AutoTrader, and completes the job with a findings document plus retail and trade figures | The findings document retained as Case evidence and a valuation entry of source `AI market research` with the retail and trade figures ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#valuation-sources)) | None on the job — the entry is a proposal on the Case and never becomes the Engineer's Value by itself; no scraping or AutoTrader integration exists inside Pegasus |

**States.** `Queued` → `Taken` → `Draft ready` → `Completed`, with `Failed`,
`Cancelled` and `Expired` as the other terminal states.

- `Queued`: created and claimable. Creation records the kind, the target
  record, and *started by* — a staff username or the connector client name.
- `Taken`: claimed by a named connector client under a lease with a visible
  expiry. A lease that expires returns the job to `Queued` and records the
  expired claim; a client may release a job back to `Queued` before then.
- `Draft ready`: the client has written its result and named it on the job;
  the job waits for staff.
- `Completed`: the staff act that consumes the result has been recorded, or
  staff completed the job by hand for a kind whose result needs no separate
  act (Query response, Unidentified-queue pass).
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
