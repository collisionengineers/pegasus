## Verification run — 2026-08-24

Local, on `task/eng-015-eva-field-values` (three commits, stacked on ENG-014).

| Suite | Result |
| --- | --- |
| `dotnet build --configuration Release` | succeeded, 0 warnings, 0 errors |
| `Pegasus.Core.Tests` | 951 passed, 0 failed |
| `Pegasus.ArchitectureTests` | 99 passed, 0 failed |
| `Pegasus.IntegrationTests` (full) | 953 passed, **1 failed**, 14 skipped, 24m35s |
| affected integration classes, after the fix | 9 passed, 0 failed |

The one integration failure was
`CustodyOutboxIntegrationTests.ExportingACaseProducesTheEvaFormatArchive`,
asserting the JSON `Reference` field equals the Pegasus case reference —
exactly what (a) changes. Updated to assert the claim number the fixture seeds
(`EXP-{fixtureId}`). Its *archive filename* assertion
(`EVA-{caseReference}.zip`) was left untouched and passed both before and after,
which is the evidence that the filename fix works.

The full integration suite has **not** been re-run end to end since that
one-line test edit; the two affected classes were re-run green and CI runs
everything. Saying so plainly rather than implying a clean full local run.

### Ticket verification checklist

- [x] EVA and QDOS suites green
- [x] `Reference` reads the claim number, not the case reference
- [x] `Inspection Address` is exactly 6 lines; image-based literal on line 1; a
      real address puts its postcode on line 6
- [x] `Accident Circumstances` carries the labelled damage area
- [x] `Mileage Unit` is `Miles` / `Km`
- [x] `Vehicle Model` carries make and model
- [x] bare `Date:` read, and incident date still correct on a letter carrying
      both — plus the suffix form (`Accident Date:`) the ticket did not name
- [x] two fragments: the appended report's inspection date wins, and the
      earliest still wins for other fields
- [x] `VAT Status` blank for QDOS — pinned
- [x] `Mileage` keeps the lookup value — pinned
- [x] the hand-off still fails closed on unaccepted evidence
      (`EvaProductionMappingBlocksMissingReadinessAndUnacceptedAddress`,
      `EvaProductionMappingFailsClosedWithoutAcceptedMappingVersion`, green)

### Reference-sample comparison

Pinned as exact-string assertions rather than eyeballed:

- `Image-based Assessment\n\n\n\n\n` — matches `AX_SP58WVO.json`.
- `109 Valley View\nHoole\n\n\n\nCH490DJ` — matches `Final Format Example 02.json`
  byte for byte, including the **unspaced** postcode. The research doc's quoted
  `OUTWARD INWARD` canonicalisation would have produced `CH49 0DJ` and broken
  this match, so it was deliberately not implemented. Raised in the PR.
