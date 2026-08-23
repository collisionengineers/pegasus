# Files

The Box read half shipped in PR #521 (release 25). This is the gallery half.

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | `ListForCaseAsync` builds `CaseEvidenceImage` with **named** arguments; `OccurrenceId` gets `occurrence.Id`. |
| `src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml` + `.cshtml.cs` | Route token and handler parameter renamed `documentId` → `occurrenceId`. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Gallery route value follows the rename. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | Same, on the file link. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Assert the gallery's id resolves against `DocumentOccurrences`. |

## The exact mechanism

```csharp
public sealed record CaseEvidenceImage(
    Guid ReceiptId, Guid AssetId, string FileName, string MediaType,
    long ContentLength, Guid? OccurrenceId = null, Guid? VersionId = null)
```

`ListForCaseAsync` constructs it **positionally**:

```csharp
new CaseEvidenceImage(
    Guid.Empty,             // ReceiptId
    occurrence.Id,          // AssetId       <- the occurrence id lands here
    version.FileName,
    version.MediaType,
    version.ContentLength,
    occurrence.DocumentId,  // OccurrenceId  <- the DOCUMENT id lands here
    version.Id)
```

Two interchangeable `Guid` slots, filled in the wrong order. The route then
receives `CaseDocuments.Id` where `EfDocumentCustodyStore` filters
`occurrence.Id == query.OccurrenceId`, and nothing matches.

## Proved live on `ap.QDOS26012`, 2026-08-23

| Request | Status |
| --- | --- |
| `…/Documents/a0e0fb9c-…/Download?versionId=782ebf4b-…` (the id the page emits — a `CaseDocuments.Id`) | **404** |
| `…/Documents/4274c589-…/Download?versionId=782ebf4b-…` (the matching `DocumentOccurrences.Id`) | **500** — reaches Box and hits [[PLAT-039]] |

The two faults are independent and both must go. The 404 never reaches Box;
the 500 is the token.

## Why the rename is part of the fix, not scope creep

The route parameter is called `documentId` but has always carried an
*occurrence* id — `_CaseDocuments.cshtml` passes `occurrence.Id` into
`asp-route-documentId`. Core's own query record names the field `OccurrenceId`
correctly. The wrong name on the boundary is what made a document id look
plausible there. Naming it for what it is stops the next caller repeating this.
