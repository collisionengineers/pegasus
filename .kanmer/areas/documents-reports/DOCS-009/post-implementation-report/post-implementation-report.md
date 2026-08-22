# Post-implementation report — DOCS-009

Commit `94b6a9dd` on `task/qdos26011-regressions`.

## What changed

`EfQueuedCustodyProcessor.cs:306` — the retained attachment's semantic role now follows its media type:

```csharp
InstructionEvidenceImages.IsImage(attachment.MediaType)
    ? DocumentSemanticRole.Image
    : DocumentSemanticRole.Instruction,
```

`InstructionEvidenceImages.IsImage` is the test this same file already applies to embedded photographs twenty lines below, so the two loops now agree instead of disagreeing about the same question.

## The migration

`20260822195419_CorrectIntakePhotographSemanticRole` corrects what is already recorded. Both directions match on `OperationKey LIKE '%:attachment:%'`, which is what makes them exact: the two retention loops stamp distinguishable keys — `:attachment:` for a file that arrived attached, `:embedded:` for a photograph found inside a PDF — and embedded photographs were always correctly `Image`, so neither direction may touch them. Verified against production data before writing: QDOS26011's ten occurrences carry `case-custody:<caseId>:attachment:<assetId>` and `…:source`.

The migration carries no `GRANT`, so `Test-AzureDeploymentPlan.ps1`'s grant-matrix gate does not apply. The pinned migration census in `IntakePersistenceIntegrationTests` was updated, which is a step CI has failed on before.

## Evidence

`AnAcceptedInstructionFilesItsPhotographsAsImagesAndItsLetterAsAnInstruction` — a new integration test driving `ProcessIntake` → accept → `IProcessQueuedCustody` with a real JPEG and a PDF on one message. Passed in 35 s. It asserts the JPEG lands as `Image` and the PDF as `Instruction`.

The JPEG is generated with SkiaSharp — already an Infrastructure dependency, so no package was added — at 709×768, the shape a genuine QDOS26011 photograph has.

## Scale of what this was hiding

On production, all eight of QDOS26011's photographs were filed as instruction documents. `EvaHandoffStore.cs:85` selects bundle images by `SemanticRole == Image`, so an export of that case would have produced an archive containing no photographs at all — the larger half of why [[CASE-019]] could not have worked even with its link repaired.
