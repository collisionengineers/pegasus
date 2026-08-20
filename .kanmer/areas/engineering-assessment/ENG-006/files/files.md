# Files — ENG-006

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | Hints/cards/captions/Working column removed; required markers; line-grid columns trimmed; damage SVG regions → toggle buttons + save form; method preselect |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | `OnPostSaveDamageAsync` (lease + `ISaveAssessment`, `assessment.impact_location`); inspection-mode read for the preselect; saved impact-location exposure |
| `src/Pegasus.Web/wwwroot/css/site.css` | `label.req::after` marker; damage-region button styling; `.line-grid` min-width reduction |
| `src/Pegasus.Web/wwwroot/js/site.js` | Damage-region click sets the hidden field / syncs the dropdown (progressive enhancement; regions are real buttons in a form) |
| `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs` or new file | Damage save round-trip + hint-free render + preselect assertions |

Reuse: `ISaveAssessment` (existing Core seam, already MCP-called), `IAcquireCaseEditLease` (already injected), `AssessmentVocabulary.ImpactLocation` (already exported to the report via `AssessmentReportProjection`).
