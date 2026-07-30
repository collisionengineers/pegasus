# Pegasus

Pegasus is Collision Engineers’ case-management and reporting domain. This glossary fixes project-specific language while canonical product and operator rules remain in the owners routed through `docs/index.md`.

## Language

**Case**:
A permanent record of Collision Engineers work. An instructed Case has a Case/PO; an Image Case has an Image Intake Reference and begins `Not ready`.
_Avoid_: Job

**Principal**:
The organisation that instructs Collision Engineers and pays for the work.
_Avoid_: Client, Work Provider, sender

**Case/PO**:
Collision Engineers’ immutable internal reference, allocated from the accepted Principal’s sequence to an instructed Case.
_Avoid_: Claim number, external reference

**Image Case**:
A Case created from image-only intake with a usable normalised VRM. It begins `Not ready`, carries an Image Intake Reference rather than a Case/PO, and may consolidate into one eligible instructed Case.
_Avoid_: Image-only intake, temporary Case

**Image Intake Reference**:
A registration-based identity allocated to an Image Case as `{normalised VRM}-{sequence}`, using a two-digit minimum (`-01`) and expanding after `-99` without reuse. It is not a Case/PO; a confirmed consolidation retains it permanently as linked history.
_Avoid_: Case/PO, external reference

**Intermediary**:
An organisation that routes work without thereby becoming the Principal.
_Avoid_: Principal, client

**Repairer**:
The vehicle holder or repair organisation associated deliberately with a Case.
_Avoid_: Principal, image sender

**Image Source**:
The actual supplier of case images, whether a Principal, Intermediary, Repairer, or individual.
_Avoid_: Sender

**Third-party vehicle evidence**:
Source evidence of a vehicle other than the Case vehicle, identified from reliable image detail. It remains retained in the Case but is excluded from Case-vehicle and report-image selection.
_Avoid_: Wrong image, unrelated evidence

**Audit**:
An Audit Case is instructed work to review another engineering firm’s original report. Its accepted reference is a lowercase `a.` or `ap.` form derived only from an unambiguous repairable or total-loss assessment in that original report; its evidence and acceptance boundary remain separate.
_Avoid_: Triage, sorting

**Inspection + Audit**:
One Case in which Collision Engineers completes its standard Inspection and then carries out a distinct Audit of that Inspection. The Audit retains its own identity, evidence, and acceptance boundary.
_Avoid_: Combined report, two-spec Inspection

**Triage**:
A distinct staff workflow for a recorded matter requiring a finding and, where applicable, exact reply-chain Sent evidence.
_Avoid_: Inbox sorting, generic sorting

**Needs sorting**:
An email-receiving outcome for material that can be persisted safely but cannot be classified into a category. It is never an Image Case or `Not ready`.
_Avoid_: Triage, Blocked intake

**Blocked intake**:
A pre-Case failure boundary where required processing, identity, limits, custody, or evidence is incomplete or unsafe.
_Avoid_: Needs sorting, Triage

**Held**:
A nonterminal Case state that pauses progression and recurring chasers pending a named staff resolution. A cancellation message creates `Held pending staff decision`; it does not itself cancel the Case.
_Avoid_: Cancelled, closed

**Created in error**:
The terminal outcome for a Case created against the wrong Principal; the original reference remains consumed and links to its replacement.
_Avoid_: Delete, reopen

**Merged**:
The terminal outcome for an Image Case whose evidence has been consolidated into one eligible pre-report instructed Case. Before report delivery, authorised staff may reasonedly reverse the merge, restoring both Cases to `Not ready` with their permanent identities and history intact.
_Avoid_: Delete, erase

**AI Proposal**:
An immutable model-generated candidate repair specification, never a report document, retained separately from the Case until a named Engineer explicitly accepts or applies it.
_Avoid_: AI assessment, automatic repair specification

**Automation Actor**:
A named non-human principal that performs one explicitly authorised Pegasus action inventory through Core use cases with its own permanent history.
_Avoid_: Service account, staff impersonation, background task

**Not ready**:
A created Case state for an Image Case or instructed Case whose ordinary business details, required source images, or other progression requirements remain incomplete. Image quality and coverage assessments are advisory and never make a Case `Not ready`.
_Avoid_: Needs sorting, Blocked intake

**Field provenance**:
The current evidential origin of a Case datum; direct values identify their source, while derived values identify their accepted inputs and calculation.
_Avoid_: Source label, value status

**Image readiness assessment**:
An advisory assessment of a Case's current image set against registration-overview, damage-close-up, and applicable reflection criteria. It is distinct from Case validity, lifecycle readiness, and report-image selection.
_Avoid_: Case validity, image validation

**Always-image-based Principal**:
A Principal whose accepted route policy waives only the image-readiness reflection advisory. It does not waive other image-readiness advisories or the report-image reflection exclusion.
_Avoid_: Image-based client, provider exception
