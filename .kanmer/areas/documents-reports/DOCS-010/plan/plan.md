# Plan

## 1. Put the occurrence id in `OccurrenceId`

In `EfIntakeReceiptStore.ListForCaseAsync`, construct the case-document
`CaseEvidenceImage` with named arguments:

```csharp
new CaseEvidenceImage(
    ReceiptId: Guid.Empty,
    AssetId: Guid.Empty,
    FileName: version.FileName,
    MediaType: version.MediaType,
    ContentLength: version.ContentLength,
    OccurrenceId: occurrence.Id,
    VersionId: version.Id)
```

`AssetId` becomes `Guid.Empty` rather than a smuggled occurrence id: for an
image served from Box there is no intake asset, and `IsCaseDocument` already
keys off `OccurrenceId`/`VersionId`, so nothing reads it. Named arguments are
the point — two adjacent `Guid` slots is how this happened, and positional
construction would let it happen again.

The intake-asset branch below keeps its positional form; there the first two
arguments genuinely are a receipt and an asset.

## 2. Name the route parameter for what it carries

`/Cases/{caseId:guid}/Documents/{documentId:guid}/Download` carries an
occurrence id. Rename the token and the handler parameter to `occurrenceId`,
and follow it in the two callers (`Details.cshtml`'s gallery,
`_CaseDocuments.cshtml`'s file link) and the log message.

The URL shape does not change — only the name — so no link, bookmark or
recorded operation is affected. `DownloadCaseDocumentQuery.OccurrenceId` in
Core already has the right name and is untouched.

## 3. Test

Extend `CustodyOutboxIntegrationTests` where the pipeline already produces a
case with confirmed photographs: assert every `CaseEvidenceImage.OccurrenceId`
the gallery would render exists in `DocumentOccurrences` for that case. That is
the assertion whose absence let a `DocumentId` sit in an `OccurrenceId` field
through two releases.

## 4. Verification

Production, after deploy: open `ap.QDOS26012`'s Evidence tab and confirm the
photographs render. This needs [[PLAT-039]] in the same release — with only
this fix the URLs stop 404-ing and start 500-ing.

## Simplification pass

To be recorded here, dated, before the PR.
