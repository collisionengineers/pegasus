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
specification, selected vehicle images, the statement and authorised
signature, and the fee note.

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
immutable, source-labelled snapshot, validates readiness and the selected
engineer identity, computes the figures once, and selects one of the four
outcomes. Infrastructure renders only that selection with the governed
template, stylesheet, logo, and signature resource.

The supplied assessment wording and the named engineer/signature evidence are
accepted only as exact matching tuples. The currently complete supplied tuple
is `A Patterson | M.Inst.IAEA | andy_patterson`; the Ed Mawdsley and Neil
O'Reilly signature images are governed assets, but no assessment may select
either until an accepted qualification completes that person's tuple. Missing,
unknown, mismatched, or substituted names, qualifications, keys, assets, source
versions, custody references, or required values fail closed. No custom
signature path, arbitrary local attachment path, placeholder, or wording absent
from the accepted evidence is permitted.

Generation returns draft assessment and fee-note artifacts with their bytes,
hashes, page counts, template version, and engine version. It is not approval,
issue, sending, external receipt, durable report-reference allocation, or
correction custody. Human approval remains required before issue; the durable
trigger, immutable reference/version and custody workflow is separately owned.

### Report-draft entry point

The renderer boundary above is reachable from one operator action (DELIV-012):
a "Generate report draft" control on the case Assessment screen
(`/Cases/{id}/Assessment`), open to the same staff roles as the rest of that
screen (Administrator, Engineer, User). It projects the case's already-saved,
confirmed assessment record into the accepted snapshot, renders it, and
returns the assessment PDF to the operator's browser. Nothing is saved,
approved, or sent by this action — it is strictly the draft-generation step
the renderer boundary above already defines; approval and issue remain the
separately owned human acts described below.

The Assessment workspace is available once the Case has entered `Report
preparation` or later (displayed "With Engineer") and a successful EVA export
or submission exists for the current Review cycle. It is never available in
`Not ready`, `Review` or `Held`; it is editable in `Report preparation` and
`Post report`, read-only in `Post-report complete`, and unavailable in the
other terminal outcomes. Returning to Review for corrected case data starts a
new cycle and requires a fresh export before the workspace opens again.

**Readiness.** A single readiness rail decides whether the control is enabled:
`AssessmentPolicy.EvaluatePostReviewReadiness` (the Assessment screen's
post-Review list) plus only requirements first introduced after the case
entered `Review`: an accepted engineer signature tuple and repair-cost figures
(below). Requirements already enforced by the transition into `Review` are not
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
custody facts, not curated: the Assessment screen's photograph
curation/ordering control is separately deferred (UI-15), so every confirmed
image on the case is offered. Their absence after entry to `Review` is an
invalid case state, not another report-readiness classification.

**Repair-cost figures are not yet derivable.** No accepted formula exists
anywhere in the domain to convert recorded estimate lines and a chosen rate
card into a numeric labour rate or paint-materials charge — the rate card is
explicitly published reference data the assessment screen never stores a
figure for, and estimate-total derivation is documented as deliberately
absent pending its own accepted authority (EXT-09, open decision D2). The
report draft does not fabricate one: until EXT-09 is accepted, every case's
readiness names "Repair cost figures" as outstanding and the control stays
disabled. This is the current, honest state of the capability, not a defect.

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
| Estimate | Assessment `Send to Claude` (With Engineer or onwards) | Direction text and a target percentage of the recorded Engineer's Value; refused without an Engineer's Value | A drafted estimate saved on the Case through the estimate tools, citing the job; state `Draft` | An Engineer accepts the draft (`Use estimate`), which makes it the Current estimate |
| Unidentified resolution | Operations `Send Unidentified to AI` for one U reference | The U reference only | A proposed destination (existing Case, new Case from an accepted instruction, Image-initiated Case, or close) and a reason | Staff confirm through the existing Unidentified resolve action; the proposal never resolves the item itself |
| Query response | A retained post-report query linked to a Case | The message reference only | Draft reply text | Offered to the composer or Case notes; never sent automatically |
| Unidentified-queue pass | An external scheduler through the Actor `create` tool — Pegasus runs no timer | The queue scope | One Unidentified-resolution proposal per item the pass examined | As Unidentified resolution, per item |

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
(opens the Assessment estimate tab), `Open query` (opens the message), or
`Review` (opens the Unidentified item) for a `Draft ready` job; `Complete
job` for a `Draft ready` Query response or Unidentified-queue pass; `Cancel`
(reason required) for any non-terminal job; otherwise nothing. `Send
Unidentified to AI` creates an Unidentified-resolution job for a chosen U
reference.

**Administration.** Automation & AI shows the active and failed job counts and
the Stop/Start automation control; that control is the ADR-0026 kill switch,
so stopping automation also stops the ledger.

### Estimate VAT on the rendered report

Each estimate carries its own VAT percentage, entered freely on the estimate
(D9). For the rendered assessment report, the Current estimate's VAT
percentage replaces the built-in repairer-VAT-registered rule; that rule
applies only when no Current estimate exists. The figures are computed once
by `Pegasus.Core`:

| Figure | Rule |
| --- | --- |
| Parts | Sum of part prices × quantity |
| Labour | Labour hours × labour rate |
| Paint | Paint hours × paint labour rate, plus paint materials |
| Other | Other costs |
| Subtotal | Parts + Labour + Paint + Other |
| VAT | Subtotal × VAT % |
| Total | Subtotal + VAT |

Signatures embedded in governed renderer documents are provenance-sensitive
document assets, not Web decorative imagery.
