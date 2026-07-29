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
Standalone reviewed work with its own evidence and acceptance boundary.
_Avoid_: Triage, sorting

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
