# Files — DOCS-009

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | At `:306`, choose the retained attachment's semantic role by media type instead of hard-coding `Instruction`. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<new>` | Correct `DocumentOccurrences.SemanticRole` for occurrences already recorded as `Instruction` whose current version carries an image media type. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Assert an accepted receipt's JPEG attachment lands as `Image` while its PDF lands as `Instruction`. |

## The one-line defect

```csharp
retained.Add(new(
    index + 2,
    attachment.FileName,
    attachment.MediaType,
    attachment.ContentLength,
    attachment.ContentHash,
    DocumentSemanticRole.Instruction,   // <- every attachment, whatever it is
    $"{casePayload.OperationKey}:attachment:{attachment.Id:N}"));
```

Twenty lines below, the embedded-photograph loop correctly passes `DocumentSemanticRole.Image`. The two loops disagree about the same question.

## Reuse

`InstructionEvidenceImages.IsImage(mediaType)` already owns "is this an image", is already the selector this same file calls for embedded photographs, and is already `public`. No second media-type test is introduced.

## The migration

`SemanticRole` is persisted as a string. The correction is a single `UPDATE` joining `DocumentOccurrences` to its current `DocumentVersions` row and setting `SemanticRole` to the `Image` code where the media type starts `image/` and the role is currently `Instruction`. `Down` is the inverse restricted to the same join, which is exact because no other route writes `Image` to an occurrence whose source is `Intake`.

Grant note: the migration writes `DocumentOccurrences`, which the Worker role already holds `SELECT, INSERT` on from `20260822044425_GrantWorkerCaseDocuments`. It carries no `GRANT`, so `Test-AzureDeploymentPlan.ps1`'s grant-matrix gate does not apply — but the migration census in `IntakePersistenceIntegrationTests` is pinned and **must** be updated, or CI fails on a collection mismatch.

## Read-only checks run

Prod, 2026-08-22, QDOS26011: eight `image/jpeg` occurrences all carry `SemanticRole = Instruction`; the `application/pdf` instruction and the `message/rfc822` original carry `Instruction` and `OriginalSource`. The same shape holds on QDOS26009 and QDOS26010.
