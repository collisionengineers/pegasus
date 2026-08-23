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

## Simplification pass — 2026-08-23

Run by hand over the branch diff (the operator's standing instruction this
session forbids delegating to the `code-simplifier` agent).

| Lens | Finding | Disposition |
| --- | --- | --- |
| **Reuse** | The gallery assertion could have been a new test with its own pipeline setup. | **Reused** the existing DOCS-009 test, which already drives a real instruction through intake, acceptance and custody to confirmed photographs. Twenty-one lines added, no second fixture. |
| **Simplification** | The two-branch `ListForCaseAsync` could be collapsed into one projection. | **Not done.** The branches read different tables for different eras of case — a Box-served document and a staging intake asset are not the same row shape, and merging them would need a union that says less than the two branches do. |
| **Altitude** | The route rename touches four files for no behaviour change. | **Kept.** The wrong name on the boundary is the cause, not a cosmetic. Leaving `documentId` there would leave the next caller the same trap, and the fix is mechanical with no URL change. |
| **Efficiency** | None. The query is unchanged; only which column reaches which field. | — |

## Deliberately not fixed here — one observation, filed as an ask

`AssetId` is now `Guid.Empty` for a Box-served image, and `ReceiptId` already
was. `CaseEvidenceImage` is really two shapes wearing one record: a staging
intake asset (receipt + asset) or a case document (occurrence + version), with
`IsCaseDocument` as the discriminator. Splitting it would be behaviour-
preserving and would make the mix-up structurally impossible rather than
merely commented against.

That is a **quality** finding, not a correctness one, so per the repository's
disposition rule it does not ride this fix. Not filed as a ticket either: the
record has one consumer and two branches, and a second implementation to prove
the split is worth having does not exist yet. Recorded here so it is a decision
rather than an omission.
