# Post-implementation report

**Branch:** `task/qdos26008-regressions` then `task/docs-008-audit-custody`
**PRs:** #505, #507 · **Commits:** `fef817b8`, `f0d8b6eb`

## Correction to this report

An earlier version of this document said the change "went further than the plan". On one
point it went **less** far, and the operator caught it: *"did you not even bother to switch
where the storage lives"*.

DOCS-007 asked for two things. Only one shipped in `fef817b8`:

| Asked for | `fef817b8` | `f0d8b6eb` |
| --- | --- | --- |
| Register intake's files as case documents | ✅ | — |
| **Serve the Evidence tab from those documents** instead of the receipt-asset endpoint | ❌ **missed** | ✅ |

The gallery had continued to read `IntakeAssets` and serve bytes from the Azure staging
blob through `DownloadIntakeAsset`. That is not cosmetic: the plan's own reasoning was that
staging blobs age out once the documents exist, so leaving the read there means the gallery
breaks the day they do. Claiming the work exceeded the plan while a named half of it was
missing was the more serious error.

## What is now built

**Layout** (`fef817b8`) — both custody routes write straight into the case folder. A
document's name carries its occurrence ordinal and, for a second revision, its revision, so
the name derives wholly from the persisted address and a read finds what the write produced
without a sidecar. `BoxDocumentContentStore` lost 106 lines: the folder resolvers, binding
builders, binding verification and role-name mapping existed only to serve the nesting.

**Records** (`fef817b8`) — `RecordRetainedCaseFilesAsync` writes the document rows for
content already in Box. Records only, never a second upload; the ordinal used for the record
is the ordinal used for the upload, which is what makes the flat name resolve at both ends.
Idempotent by operation key.

**Read path** (`f0d8b6eb`) — `ListForCaseAsync` returns the case's document occurrences and
the gallery links to `/Cases/Documents/Download`, which in Production resolves through
`BoxDocumentContentStore`. A case accepted before those records existed still renders from
its retained asset rather than going blank — the additive transition the plan required, and
it ends with the last pre-DOCS-007 case rather than persisting as a second route.

## Operator direction that shaped it

> "I never even defined a box layout, I think Claude made it up. Have everything go under
> the main case/PO folder for files. WHat the fuck is the document route?? Why is there a
> seperate route called documents, that the documents dont use?"

Both halves were correct. The nesting was never asked for, and the intake custody route
wrote files to Box with **no records at all** — which is why the Document custody panel
said "No document occurrences are retained" while the files sat in Box.

## Evidence

- `Pegasus.Core.Tests` — 916 passed
- Custody, case-details and image-viewing integration tests — 46 passed
- `BoxDocumentContentStoreTests`, `ProductionBoxCustodyTests` cover the flat layout and a
  second revision not colliding

## Not proved

The live Evidence gallery has **not** been seen serving from Box, because custody is
failing on both production audits ([[DOCS-008]]) and no case has document records yet. Until
that is fixed, the read path is proved by tests and by reading the code, not by production.
