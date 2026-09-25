# Pegasus

Pegasus is Collision Engineers’ case-management and reporting domain. This glossary fixes project-specific language while canonical product and operator rules remain in the owners routed through `docs/index.md`.

## Language

**Case**:
A permanent record of Collision Engineers work. An Instruction-initiated Case is the formal record created after Principal, Case type, and identity-critical gates settle; a Triage Case is a Case whose request is not a definitive instruction; an Image-initiated Case is a separate image-first projection with no Case/PO.
_Avoid_: Job

**Principal**:
The organisation that instructs Collision Engineers and pays for the work.
_Avoid_: Client, Work Provider, sender

**Case/PO**:
Collision Engineers’ immutable internal reference, allocated from the accepted Principal’s sequence to a Case: no prefix for an Inspection or Inspection + Audit Case (`QDOS26001`), `a.` for a standalone Audit (`a.QDOS26002`) and `t.` for a Triage (`t.QDOS26003`). The Audit reference of an Inspection + Audit Case (`a.QDOS26001`) names its Audit report; it is not a Case/PO and consumes no number.
_Avoid_: Claim number, external reference

**Received date**:
The Europe/London date a Case was received: its instruction's receipt, or its creation when staff create it directly. It is the Case's only instruction date: EVA's `Instruction Date` and the date the report says instructions were received ([FRD-23](docs/frd/frd-23-case-draft-fields-provenance-and-global-checks.md#instruction-field-meanings)).
_Avoid_: Instruction date (as a separate fact), processed date

**Image intake**:
A durable Image-initiated Case projection for image-only material with a usable normalised VRM. It carries an Image Intake Reference, may merge into one eligible instructed Case, and otherwise awaits definitive instruction or is staff-closed with a reason; it never becomes a formal Case/PO.
_Avoid_: Image Case, temporary Case

**Image Intake Reference**:
A registration-based identity allocated to an Image intake as `{normalised VRM}-{sequence}`, using a two-digit minimum (`-01`) and expanding after `-99` without reuse. It is not a Case/PO; confirmed association retains it permanently as linked history.
_Avoid_: Case/PO, external reference

**Intermediary**:
An organisation that routes work without thereby becoming the Principal.
_Avoid_: Principal, client

**Repairer**:
A reusable organisation with name, full address, and contacts that may relate to multiple Principals and is deliberately associated with a Case as its vehicle holder or repair organisation.
_Avoid_: Principal, image sender

**Image Source**:
The actual supplier of case images, whether a Principal, Intermediary, Repairer, or individual.
_Avoid_: Sender

**Third-party vehicle evidence**:
Source evidence of a vehicle other than the Case vehicle, identified from reliable image detail and recorded by staff as the Third party image tag. It remains retained in the Case but is excluded from Case-vehicle selection and the EVA image bundle.
_Avoid_: Wrong image, unrelated evidence

**Audit**:
An Audit Case is instructed work to review another engineering firm's original
report. A standalone Audit Case's Case/PO is `a.` plus its own number from
the Principal's sequence, for example `a.QDOS26002`. It is created with or
without that report. The assessment outcome is recorded on the Case, not in
its identity. When no original report is filed and none was kept at intake,
**Original report missing** stays outstanding until staff mark a filed
document as the original report ([FRD-01](docs/frd/frd-01-case-identity-and-lifecycle.md)).
The Audit of an Inspection + Audit Case is part of that Case, not an Audit
Case (below).
_Avoid_: Triage, sorting

**Inspection + Audit**:
One Case holding an Inspection and, once its Inspection report is sent, its Audit. Collision Engineers completes its standard Inspection on the Case (for example `QDOS26001`). Create audit then adds the Audit to the same Case: a separate copy of the Case's values that only the Audit edits, with its own report under the Audit reference `a.` plus the same Case/PO (`a.QDOS26001`), its own fee note and an `a.` Box subfolder. The Case keeps one state, one Files and one Notes; no second Case and no second number is created ([FRD-01](docs/frd/frd-01-case-identity-and-lifecycle.md)).
_Avoid_: Combined report, two-spec Inspection, linked Audit Case

**Triage**:
A Case type for an assessment request that is not a definitive instruction.
Its Case/PO is `t.` plus the next number from the Principal's shared sequence
(for example `t.QDOS26003`), allocated only once its Principal and
registration are established. It follows its own Triage states; completion
records a decided outcome, and Reply with outcome is optional editable email.
A later definitive instruction is a separate Case with its own number, which
the Triage may link to ([FRD-03](docs/frd/frd-03-triage.md)).
_Avoid_: pre-Case Triage record, T-reference

**Unidentified**:
Safely retained material or an inseparable submission group that has not become a Case (Triage included) or Image intake: its identity, meaning, ownership or destination cannot be established, or it could not be read. It receives an immutable U-reference and a reason under FRD-02; material that could not be read carries the reason Could not be read with its file kind. Readable material that must not become a Case is closed with a reason, and a closed item can be reopened. It is distinct from Triage, a missing Audit original report, Image Intake and a formal Case in Not ready.
_Avoid_: Triage, Blocked, Blocked intake

**Held**:
A Case state that pauses progression and recurring chasers until staff decide. A hold records a reason and may carry a Review on date, which is when it is next due for a decision. A cancellation message never changes a Case by itself; staff may place the Case on Hold with the cancellation as the reason ([FRD-13](docs/frd/frd-13-case-lifecycle-and-workflow.md)).
_Avoid_: Cancelled, closed

**Created in error**:
A reasoned disposition for a Case created against the wrong Principal; its
immutable reference remains consumed and links to its replacement. It does not
create a terminally closed Case.
_Avoid_: Delete, reopen

**Associated**:
The settled outcome for an Image intake whose evidence has been linked to one eligible pre-report instructed Case; it becomes final at report delivery. Before report delivery, authorised staff may reasonedly reverse the association; the intake reference, Case identity, source evidence, and relationship history remain permanent.
_Avoid_: Merged, delete, erase

**AI Proposal**:
An immutable model-generated candidate repair specification, never a report document, retained separately from the Case until an authorised human staff member explicitly accepts or applies it.
_Avoid_: AI assessment, automatic repair specification

**Estimate document**:
The non-retained PDF presentation of one saved estimate version and its
Core-owned calculations. It is not a report, approval, delivery or source
estimate artifact.
_Avoid_: Estimate report, generated report

**Automation Actor**:
A named non-human principal that performs one explicitly authorised Pegasus action inventory through Core use cases with its own permanent history.
_Avoid_: Service account, staff impersonation, background task

**Send to AI**:
The stable staff-triggered work handoff governed by FRD-10. It may return proposals and perform explicitly permitted, attributed writes through Core, each the Case's value shown with its AI source tag. It never records professional findings or sends outward correspondence. AiWork push and AiJobs pull remain distinct accepted transports.
_Avoid_: Send to Claude, AI assessment, automatic report

**First sent to Engineer**:
The once-per-Case handoff proxy governed by [FRD-07](docs/frd/frd-07-eva-and-external-engineering-handoff.md). Native handoff ([FRD-13](docs/frd/frd-13-case-lifecycle-and-workflow.md)) and optional EVA are distinct routes; the proxy is not external receipt, delivery or report-sent evidence.
_Avoid_: Sent to Engineer (the activity count), report sent

**Sent to Engineer today/week**:
The Operations activity count of `First sent to Engineer` proxy events within the Europe/London day or Monday-based week. A count of events is not the once-per-Case proxy itself.
_Avoid_: First sent to Engineer (the per-Case event), reports sent

**New cases today**:
The Operations metric for instructed Cases created since Europe/London midnight, including Cases later completed or given a cancellation/rejection disposition that day and excluding Image intakes, Triage Cases and `Unidentified`.
_Avoid_: In today, Due today, received today

**Not ready**:
A created Case state for an instructed Case whose ordinary business details, required source images, or other progression requirements remain incomplete. Image quality and coverage assessments are advisory and never make a Case `Not ready`; pre-Case Image intake is not a Case state.
_Avoid_: Unidentified

**Review**:
The Case state reached automatically when every required instruction item and image is present. Staff hand a Review Case to an Engineer; there is no separate act of reviewing instructions or images ([FRD-13](docs/frd/frd-13-case-lifecycle-and-workflow.md)).
_Avoid_: Reviewed checkbox, staff review gate

**Field provenance**:
The current evidential origin of a Case datum; direct values identify their source, while derived values identify their accepted inputs and calculation.
_Avoid_: Source label, value status

**Image readiness assessment**:
An advisory assessment of a Case's current image set against registration-overview, damage-close-up, and applicable reflection criteria. It is distinct from Case validity, lifecycle readiness, and report-image selection.
_Avoid_: Case validity, image validation

**Always-image-based Principal**:
A Principal whose persisted inspection-mode setting autofills `Image Based Assessment` as the inspection address at Case creation (authorised staff may override to an explicit location on the specific Case with a reason) and waives only the image-readiness reflection advisory. It does not waive other image-readiness advisories or the report-image reflection exclusion.
_Avoid_: Image-based client, provider exception

**Image Based Assessment**:
The exact report value recorded instead of a physical inspection address when a Case is assessed from images alone; always written out in full in staff-facing surfaces and documents.
_Avoid_: IBA, image-based mode, desktop assessment value

**Vehicle enrichment**:
The acquisition of externally sourced vehicle observations after case intake to enhance, but never silently replace, Case data.
_Avoid_: Vehicle-data integration, automatic correction

## Interface vocabulary

The domain names above are the language of the code. Several of them are not
what an operator reads: the interface layer maps them through
`Pegasus.Web.Presentation.OperatorLabels`, and both layers are correct in
their own place. Internal identifiers (`Intake*`, `ImageIntake*`, and the
rest) are unchanged by this mapping — the interface ban is on what an
operator sees, not on how the code is named.

| Domain term | Interface term |
| --- | --- |
| Intake receipt | Received file (an Intake log row) |
| Intake queues | E-mail activity |
| Image intake | Vehicle images |
| Image Intake Reference | Image reference |
| State (case filter) | Case stage |

The word “intake” never appears in operator-facing text (operator decision
2026-08-04), except the Administrator's **Intake log** tab name (13 September
2026). `Review` and `Ready to review` denote the Case stage only.


**Completed / Query**:

Reversible post-report Case states. A query received for or attached to a
Completed Case moves it to Query; replying moves it back to Completed.
There is no terminally closed Case state
([FRD-13](docs/frd/frd-13-case-lifecycle-and-workflow.md)).
