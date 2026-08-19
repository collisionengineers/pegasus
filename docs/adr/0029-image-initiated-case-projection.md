---
id: ADR-0029
status: accepted
date: 2026-08-19
supersedes: [ADR-0013]
superseded_by: []
related_capabilities: [INT-17, INT-28]
related_frd: [frd-01, frd-02, frd-05, frd-06, frd-12]
tags: [image-initiated, image-intake, lifecycle]
---
# ADR-0029: Image-initiated Case projection

## Status

Accepted. This ADR supersedes the image-only pre-Case technical boundary in
ADR-0013. ADR-0013 remains an immutable historical record; its accepted body is
not edited.

## Context

Vehicle images can arrive before formal instructions. ImageIntake already
allocates an immutable per-VRM reference and retains source identity, while
formal Case allocation requires a Principal and Case/PO gates. Treating the
image record as a formal Case would weaken those gates; treating it as an
unnamed holding record loses searchability, merge history, and custody.

## Decision

Image-initiated Case is a named lifecycle projection over ImageIntake, not a
row in the formal Cases table. Its VRM reference is immutable and separate from
Case/PO, Audit, and Unidentified references. The projection uses three outcomes:
Awaiting instruction, Merged into Instruction-initiated Case, and Staff-closed
with a reason. A unique, non-overlapping VRM match records a merge event on both
the ImageIntake history and formal Case history; staff closure is reasoned and
terminal.

No second Box client, runtime, database, or formal Case allocator is
introduced: Image-initiated files stay under the existing intake
source-artifact retention until a merge makes them available for the formal
Case's own Box custody. A dedicated VRM-keyed custody root for the
Image-initiated Case itself is not part of this decision — it has no caller in
this slice, so it is not built and not claimed; a future ADR covers it if a
concrete caller needs it.

## Consequences

- Formal Instruction-initiated Cases remain the only Case/PO allocation path.
- Search and Case history show both origins without changing either identity.
- Lifecycle state and event history require additive SQL projection data and
  replay/CAS handling.
- Conflicting or unreadable image groups remain INTK-007 Unidentified work and
  never receive a fabricated Image-initiated reference.

## Links

- [FRD-01](../frd/frd-01-case-identity-and-lifecycle.md)
- [FRD-02](../frd/frd-02-intake-and-source-identity.md)
- [FRD-05](../frd/frd-05-documents-extraction-and-custody.md)
- [FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md)
- [FRD-12](../frd/frd-12-operator-experience.md)
