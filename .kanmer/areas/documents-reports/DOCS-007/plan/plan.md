# Plan

Committed in `fef817b8`.

## Operator direction

> "I never even defined a box layout, I think Claude made it up. Have everything go under
> the main case/PO folder for files. .eml, instructions, original report if audit. WHat
> the fuck is the document route?? Why is there a seperate route called documents, that
> the documents dont use? this sounds like bad codebase"

Both halves were correct, and the second was the more serious finding.

## Root cause — two routes, and the documents did not use the one named for documents

- The **intake custody route** put the source and its attachments under
  `<case>/Evidence/Original instruction/` and wrote **no records at all**. That is why the
  case's Document custody panel said "No document occurrences are retained" while the
  files sat in Box. This is the operator's "files not stored in case evidence".
- The **managed document route** — the one that creates those records — wrote to
  `<case>/Evidence/<Role>/<NNN name>/Revision NNN/<name>` plus two JSON binding sidecars:
  three folders and two extra files wrapping every single document.

## The change

1. Both routes write straight into the case folder.
2. A document's name carries its occurrence ordinal and, for a second revision, its
   revision. The name is derived wholly from the persisted address, so a read finds what
   the write produced without a sidecar to point the way.
3. Intake's files are recorded as case documents, so the case can list and open them. The
   content is already in Box, so this writes **records only** and never uploads twice. The
   ordinal used for the record is the ordinal used for the upload — that is what makes the
   flat name resolve at both ends. Idempotent by operation key, because custody work
   retries.
4. Folded image-case files join the same folder using the collision rule already there.

## Acceptance

- `.eml`, instruction and original report sit flat in the case/PO folder. ✅ (tests)
- The case lists and opens them. ✅ (tests)
- A second revision of one occurrence does not collide. ✅
- A retried custody operation writes the records once. ✅
- Nothing is uploaded twice. ✅
- Live: the Evidence tab served from Box — Phase 6.

## Simplification pass

2026-08-21. This ticket **is** a simplification: `BoxDocumentContentStore` loses 106 lines
— the folder resolvers, binding builders, binding verification and role-name mapping that
only the nesting needed — and one of the two rival custody routes stops existing as a
separate shape.

Two related items are deliberately left to [[PLAT-032]] rather than pulled in here, both
named so neither is silently skipped:
- the `RetainAccepted*` overload pairs duplicated across `BoxCaseCustody` and
  `LocalCaseCustody`;
- the three definitions of "the case's images" (`InstructionEvidenceImages`,
  `ICaseEvidenceImageQueries`, the EVA store's own `DocumentOccurrence` query). This
  change converges two of them; confirming which becomes dead belongs with the sweep.

The ticket body's `[[SIMPLI-016]]` reference was wrong — the sweep ticket is
[[PLAT-032]].
