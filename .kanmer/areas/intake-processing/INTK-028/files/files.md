# Files

Committed in `1a86f5db`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | Speedo rule de-anchored from line start; vehicle, speedo and registration values cut at the next column label through one shared `ReportColumnCutPattern` constant; whole-line third-party guard; `IsReportFragment` removed; policy version 4 → 5 | the vehicle rule's existing column-cut label list, promoted to a shared constant |
| `tests/Pegasus.Core.Tests/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicyTests.cs` | The real multi-column line; `Speedo: Miles` with no digits still abstains; comma-thousands; a report whose file name says nothing; third-party rows never feed claimant fields | existing policy test harness |

## Why no report-file test

The original report is written by a different third-party engineering firm each time and
named however that firm's system named it, so `IsReportFragment`'s "report" in the file
name never identified one. The test is **removed**, not widened: the report grammar now
runs over every fragment and is written so only a report can satisfy it, with its facts
appended last so the instruction letter still outranks.

Generalised into [[INTK-031]], which builds the issuer corpus this points at.
