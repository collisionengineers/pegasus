# Pegasus

Pegasus is Collision Engineers’ case-management and reporting domain. This glossary fixes project-specific language while canonical product and operator rules remain in the owners routed through `docs/index.md`.

## Language

**Case**:
A permanent record of accepted Collision Engineers work. It is created only after its required identity and acceptance evidence is settled.
_Avoid_: Job

**Principal**:
The organisation that instructs Collision Engineers and pays for the work.
_Avoid_: Client, Work Provider, sender

**Case/PO**:
Collision Engineers’ immutable internal case reference, allocated from the accepted Principal’s sequence.
_Avoid_: Claim number, external reference

**Intermediary**:
An organisation that routes work without thereby becoming the Principal.
_Avoid_: Principal, client

**Repairer**:
The vehicle holder or repair organisation associated deliberately with a Case.
_Avoid_: Principal, image sender

**Image Source**:
The actual supplier of case images, whether a Principal, Intermediary, Repairer, or individual.
_Avoid_: Sender

**Audit**:
Standalone review of another engineer's report with its own evidence and acceptance boundary. In an Inspection + Audit Case, the Audit remains distinct follow-on work.
_Avoid_: Triage, sorting

**Inspection + Audit**:
One Case in which Collision Engineers completes an Inspection report and then immediately performs a distinct Audit of that report.
_Avoid_: Combined report, two-spec Inspection

**Triage**:
A distinct staff workflow for a recorded matter requiring a finding and, where applicable, exact reply-chain Sent evidence.
_Avoid_: Inbox sorting, generic sorting

**Needs sorting**:
A receiving outcome for evidence that can be persisted safely but cannot yet be routed.
_Avoid_: Triage, Blocked intake

**Blocked intake**:
A pre-Case failure boundary where required processing, identity, limits, custody, or evidence is incomplete or unsafe.
_Avoid_: Needs sorting, Triage

**Created in error**:
The terminal outcome for a Case created against the wrong Principal; the original reference remains consumed and links to its replacement.
_Avoid_: Delete, reopen

**AI Proposal**:
An immutable model-generated candidate repair specification, never a report document, retained separately from the Case until a named Engineer explicitly accepts or applies it.
_Avoid_: AI assessment, automatic repair specification

**Automation Actor**:
A named non-human principal that performs one explicitly authorised Pegasus action inventory through Core use cases with its own permanent history.
_Avoid_: Service account, staff impersonation, background task

**Not ready**:
A created Case state for an instructed Case whose ordinary business details or images remain incomplete after safe identity allocation.
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
