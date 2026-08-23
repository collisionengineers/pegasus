# Post-implementation report

**PR #521** (Box media-type half, release 25) and **PR #523** (gallery half,
release 26 at `7d6a948a`). This report covers the second half; the first is
recorded in the ticket body.

## What shipped

`EfIntakeReceiptStore.ListForCaseAsync` builds the case-document
`CaseEvidenceImage` with **named** arguments, so `OccurrenceId` receives
`occurrence.Id` and not `occurrence.DocumentId`. `AssetId` is `Guid.Empty`:
there is no intake asset behind an image served from Box.

The route token and handler parameter are renamed `documentId` → `occurrenceId`
across `Download.cshtml`, `Download.cshtml.cs`, `Details.cshtml` and
`_CaseDocuments.cshtml`. The URL shape is unchanged; only the name is, because
the wrong name on the boundary is what made a document id look plausible there.

## Evidence

- `CustodyOutboxIntegrationTests` now asserts every gallery `OccurrenceId`
  resolves against `DocumentOccurrences` for the case — the assertion whose
  absence let a `DocumentId` sit in an `OccurrenceId` field through two
  releases.
- The independent reviewer searched repository-wide for `asp-route-documentId`,
  `documentId =`, `"documentId"`, `LinkGenerator` and `RouteValues[` across
  `.cshtml`, `.cs`, `.js`, `.md` and `.http`: **zero** remaining references.
- CI green on `ce4d646c`, including the browser job.

## Deviations from plan

None.
