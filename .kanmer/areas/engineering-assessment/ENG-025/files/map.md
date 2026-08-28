# Files — ENG-025

## Owned (this ticket changes)

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentWorkspace.cs` | D11 access policy: `CanOpen` state set ReportPreparation/PostReport/PostReportComplete + current export; add read-only derivation |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | full port to context.md §1.9 (assessment-v3, evidence rail, record bar, dialogs); old sections removed |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | gate + unavailable surface, ribbon data, evidence rail queries, Send to Claude via `ICreateAiJob`, preview handler; damage/section handlers removed |
| `src/Pegasus.Web/Pages/Cases/Assessment/Suggestions.cshtml` | check; expected unchanged |
| `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs` | D11 theory rows |
| `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` | retarget to ported page |
| `tests/Pegasus.IntegrationTests/AssessmentDamageAndCopyWebTests.cs` | copy-discipline assertions kept against new page; damage/section journeys removed with their handlers |
| `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs` | retarget prefill cascade assertions to the ribbon display |
| `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs` | fake access state moves Review → ReportPreparation (D11) |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | matches `Assessment*WebTests.cs` glob; fake access state rows if needed |
| `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs` | retarget to the new readiness surface (report-draft condition) |

## Reused (read-only, named)

`Pages/Shared/_StatusChip`, `_EvidenceViewer`, `_EditHeartbeat`,
`_EditFinishConfirm`, `_ReasonDialog`; `Pages/Cases/Details.cshtml` patterns
(record-bar/gated/dialog frame, evidence URL rules);
`Presentation/OperatorLabels.cs` labels; `wwwroot/css/site.css` §1.9 block;
`wwwroot/js/site.js` dialog/tablist/range/rail modules.

## Explicitly NOT touched

`src/Pegasus.Web/wwwroot/js/site.js` (no new module needed — every retained
behaviour already has a site.js pattern; CSP rule satisfied by deletion of
the inline blocks), `site.css`, `Pages/Cases/Details.*` (its Open Assessment
gate fixes itself through the Core policy), `Core/AiWork/**`,
`Core/Assessment/Estimates.cs` (ENG-026 lane, not on branch),
`Migrations/**`.
