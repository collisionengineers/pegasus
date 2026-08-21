# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commit:** `fef817b8`

## What was built

Both custody routes now write straight into the case/PO folder. A document's name carries
its occurrence ordinal and, for a second revision, its revision — derived wholly from the
persisted address, so a read finds what the write produced without a sidecar to point the
way. Intake's files are recorded as case documents, so the case can list and open them.

`BoxDocumentContentStore` loses 106 lines: `ResolvePlainFolderAsync`,
`ResolveBoundFolderAsync`, `VerifyBindingAsync`, `OccurrenceBinding`, `VersionBinding`,
`RoleName` and the binding constants all existed only to serve the nesting.

## Departure from the plan, on operator direction

The plan proposed registering through `IAddCaseDocument` and switching the Evidence tab
read to `IDownloadCaseDocument`. The operator rejected the premise underneath it:

> "I never even defined a box layout, I think Claude made it up. Have everything go under
> the main case/PO folder for files. .eml, instructions, original report if audit. WHat
> the fuck is the document route?? Why is there a seperate route called documents, that
> the documents dont use? this sounds like bad codebase"

Both halves were correct. The layout was never asked for — three folders and two JSON
binding sidecars wrapped every single document. And the second half named the real defect:
the intake custody route wrote files to Box and **no records at all**, which is why the
Document custody panel said "No document occurrences are retained" while the files sat in
Box.

So the change went further than the plan: flatten the layout as well as write the records.

## Records only, never a second upload

`RecordRetainedCaseFilesAsync` writes `CaseDocumentEntity` / `DocumentVersionEntity` /
`DocumentOccurrenceEntity` for content that is already in Box. The ordinal used for the
record is the ordinal used for the upload — that is what makes the flat name resolve at
both ends. Idempotent by operation key, because custody work retries.

## A correction folded in

This commit also corrected [[PLAT-031]] from the previous commit: hiding the EVA panel by
returning no preparation made the MCP status tool report a case that exists as "not
found". `null` goes back to meaning no such case.

## Evidence

- `Pegasus.Core.Tests` — 908 passed
- `BoxDocumentContentStoreTests`, `ProductionBoxCustodyTests` updated for the flat layout,
  including a second revision not colliding
- Full integration suite: recorded before merge
- Live: `.eml`, instruction and original report flat in the case folder, listed and
  openable from the case — Phase 6

## Deferred, and named

The `RetainAccepted*` overload pairs across `BoxCaseCustody`/`LocalCaseCustody`, and
confirming which of the three definitions of "the case's images" is now dead, are on
[[PLAT-032]]. The ticket body's `[[SIMPLI-016]]` reference was wrong — the sweep ticket is
[[PLAT-032]].
