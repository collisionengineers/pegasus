# Proof — DOCS-009

Release 23, source `b6d54ff6665253a4abe01c646bd64f238b15e24b`, image `sha256:7193802c37d6d448cad8e96293d5b4c71e463d7a62b4e6dccfb4fe7d3700eb36`, revision `pegasus-prod-web-252ow37gij--b6d54ff6-eva`. Deployed and smoked 2026-08-22.

## Evidence tier: production data, read back after the migration ran

The migration `20260822195419_CorrectIntakePhotographSemanticRole` was applied to the production database by `efbundle.exe` on 2026-08-22, and `__EFMigrationsHistory` records it as head.

`DocumentOccurrences` for the two cases that had retained photographs, read back afterwards:

| Case | `Image` | `Instruction` | `OriginalSource` |
| --- | ---: | ---: | ---: |
| QDOS26011 | **8** | 1 | 1 |
| QDOS26010 | **15** | 2 | 1 |

QDOS26011's eight JPEG photographs were `Instruction` before the release and are `Image` after. Its instruction PDF is still `Instruction` and its `.eml` is still `OriginalSource` — the two things that had to be left alone.

QDOS26010's 15 comprise its six corrected attachments plus the nine embedded photographs that were already `Image` and were **not** touched, which is what the `:attachment:` operation-key predicate exists to guarantee.

## The prediction was made before the write

The migration's exact predicate was run against production as a read-only `SELECT` before shipping, and forecast 8 rows on QDOS26011, 6 on QDOS26010, 9 embedded photographs untouched and 3 PDFs left as `Instruction`. The post-migration counts match that forecast exactly.

## Code path

`EfQueuedCustodyProcessor` now chooses the role by media type through `InstructionEvidenceImages.IsImage` — the same test the file already applied to embedded photographs, so its two retention loops agree instead of disagreeing.

`AnAcceptedInstructionFilesItsPhotographsAsImagesAndItsLetterAsAnInstruction` drives `ProcessIntake` → accept → `IProcessQueuedCustody` with a real SkiaSharp-generated JPEG and a PDF on one message, and asserts the roles they land with. Passed locally and in CI (`sql-integration`, all three shards green on the merged commit).

## What this unblocked

`EvaHandoffStore` selects bundle images by `SemanticRole == Image`. Before this, an export of QDOS26011 would have produced an archive containing no photographs at all. The Evidence tab's eligibility column also reads the same role, so all eight photographs previously displayed as "Not an image".

## Not claimed

That an operator has downloaded an archive containing these eight images. That is [[CASE-019]]'s proof and needs a signed-in browser session.
