# Pegasus

Pegasus is Collision Engineers’ case-management and reporting domain. This glossary fixes project-specific language while canonical product and operator rules remain in the owners routed through `docs/index.md`.

## Language

**Case**:
A permanent record of Collision Engineers work. An Instruction-initiated Case is the formal record created after Principal, Case type, and identity-critical gates settle; an Image-initiated Case is a separate image-first projection with no Case/PO.
_Avoid_: Job

**Principal**:
The organisation that instructs Collision Engineers and pays for the work.
_Avoid_: Client, Work Provider, sender

**Case/PO**:
Collision Engineers’ immutable internal reference, allocated from the accepted Principal’s sequence to an instructed Case.
_Avoid_: Claim number, external reference

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
Source evidence of a vehicle other than the Case vehicle, identified from reliable image detail. It remains retained in the Case but is excluded from Case-vehicle and report-image selection.
_Avoid_: Wrong image, unrelated evidence

**Audit**:
An Audit Case is instructed work to review another engineering firm’s original report. A definitive instruction creates the normal Case/PO without confirmation; its lowercase `a.` or `ap.` Audit reference is derived later only from an unambiguous repairable or total-loss assessment in that original report.
_Avoid_: Triage, sorting

**Inspection + Audit**:
One Case in which Collision Engineers completes its standard Inspection and then carries out a distinct Audit of that Inspection. The Audit retains its own identity, evidence, and acceptance boundary.
_Avoid_: Combined report, two-spec Inspection

**Triage**:
A distinct pre-Case staff workflow requiring a finding and, where applicable,
exact reply-chain Sent evidence. Its global increasing `T-00001` reference has
no yearly or Principal reset and is never reused. Formal instructions create a
normal Case/PO and link the Triage; Triage itself allocates neither.
_Avoid_: Inbox sorting, generic sorting

**Unidentified**:
A safe pre-case routing state for persisted material that cannot yet progress: unclassifiable email, and recorded Triage material awaiting a vehicle registration. It is never a pre-Case Image intake or `Not ready`.
_Avoid_: Triage, Blocked intake

**Blocked intake**:
A pre-Case failure boundary where required processing, identity, limits, custody, or evidence is incomplete or unsafe.
_Avoid_: Unidentified, Triage

**Held**:
A nonterminal Case state that pauses progression and recurring chasers pending a named staff resolution. A cancellation message creates `Held pending staff decision`; it does not itself cancel the Case.
_Avoid_: Cancelled, closed

**Created in error**:
The terminal outcome for a Case created against the wrong Principal; the original reference remains consumed and links to its replacement.
_Avoid_: Delete, reopen

**Associated**:
The settled outcome for an Image intake whose evidence has been linked to one eligible pre-report instructed Case; it becomes final at report delivery. Before report delivery, authorised staff may reasonedly reverse the association; the intake reference, Case identity, source evidence, and relationship history remain permanent.
_Avoid_: Merged, delete, erase

**AI Proposal**:
An immutable model-generated candidate repair specification, never a report document, retained separately from the Case until a named Engineer explicitly accepts or applies it.
_Avoid_: AI assessment, automatic repair specification

**Automation Actor**:
A named non-human principal that performs one explicitly authorised Pegasus action inventory through Core use cases with its own permanent history.
_Avoid_: Service account, staff impersonation, background task

**Send to AI**:
The stable user-triggered domain action that creates a later AI work request and may return a proposed repair specification for named-Engineer review. Provider wording does not rename the action or permit direct Case mutation.
_Avoid_: Send to Claude, AI assessment, automatic report

**First sent to Engineer**:
The once-per-Case proxy event recorded when the Case's first successful EVA export bundle is generated. It is generation evidence, never receipt, delivery, or report-sent evidence, and it never repeats for the same Case.
_Avoid_: Sent to Engineer (the activity count), report sent

**Sent to Engineer today/week**:
The Operations activity count of `First sent to Engineer` proxy events within the Europe/London day or Monday-based week. A count of events is not the once-per-Case proxy itself.
_Avoid_: First sent to Engineer (the per-Case event), reports sent

**New cases today**:
The Operations metric for instructed Cases created since Europe/London midnight, including Cases later closed that day and excluding Image intakes, Triage, `Unidentified`, and `Blocked intake`.
_Avoid_: In today, Due today, received today

**Not ready**:
A created Case state for an instructed Case whose ordinary business details, required source images, or other progression requirements remain incomplete. Image quality and coverage assessments are advisory and never make a Case `Not ready`; pre-Case Image intake is not a Case state.
_Avoid_: Unidentified, Blocked intake

**Review**:
A Case state in which staff manually review its readiness and accepted evidence before Engineer-queue eligibility or direct Engineer assignment.
_Avoid_: Automatic approval, Engineer assignment

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
| Intake receipt | Received item |
| Intake queues | E-mail activity |
| Blocked intake | Blocked |
| Image intake | Vehicle images |
| Image Intake Reference | Image reference |
| State (case filter) | Case stage |

The word “intake” never appears in operator-facing text (operator decision
2026-08-04). `Review` and `Ready to review` denote the Case stage only.
