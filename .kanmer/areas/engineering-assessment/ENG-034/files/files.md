# Files — ENG-034 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

Wrapper note: the two `docs/design/test-ui/**` rows were added by the Claude
wrapper after confirming `scripts/Test-UiCatalogue.ps1` (line 20 allows
`visual | redirect | download | protocol`; line 37 rejects an unclassified
routed source) and the current `visual` entry for `Pages/Cases/Assessment/
Index.cshtml` at `docs/design/test-ui/catalogue.json` line ~289. Codex's
original list handed that edit to UIIMP-014; that would break the catalogue
check on ENG-034's own PR. Every path below was confirmed to exist (or not,
for `create`) with `ls` in the main checkout at `cad00be9`.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | change | Retire the rendered Assessment workbench while retaining the route stub. | Existing Razor route declaration. |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | change | Replace `OnGetAsync` with the permanent redirect to `/Cases/{id}?section=estimate`; remove relocated handler ownership after CASE-038 hosts it. | `RedirectPermanent` route-stub pattern (`Pages/Triage/Index.cshtml.cs`, `Pages/Unidentified/Index.cshtml.cs`). |
| `src/Pegasus.Web/Pages/Cases/Assessment/Suggestions.cshtml` | change | Retarget its Back link (line 37, `/Cases/{id}/Assessment`) to the Estimate section so it does not navigate to the retired page. | Case `?section=estimate` route. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | create | Read-only D30 Damage shell using only currently projected scalar values. | `AssessmentVocabulary`, Case partial display conventions (`_CaseVehicle.cshtml`, `_CaseSummary.cshtml`). |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` | create | Move the ENG-028 estimate tabs, editor, totals, whole-page import and Send to Claude. | Existing `Assessment/Index.cshtml` markup, `EstimateTotals.Compute`, `EstimatePolicy`, existing handlers hosted per the CASE-038 contract. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` | create | Read-only shell for existing outcome/salvage/cost values; no ENG-029 editor scope. | `AssessmentVocabulary` and `OperatorLabels`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` | create | Read-only report shell and, if the frame contract supplies the handler host, the moved existing draft/preview controls. | `AssessmentReportProjection.Prepare`; existing report-draft handlers. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Add ENG-034-owned section headings, placeholders and the presentation labels used by its four partials. Capacity-one lease. | `OperatorLabels` as the sole operator vocabulary owner. |
| `docs/design/test-ui/catalogue.json` | change | Reclassify the `Pages/Cases/Assessment/Index.cshtml` entry from `visual` (`pages/case-assessment--default.html`) to `redirect` with a reason, matching the PLAT-029 route-stub entries. Capacity-one lease. | Existing `redirect` entries (`/Triage`, `/Unidentified`, `/Administration/MailCategories`). |
| `docs/design/test-ui/pages/case-assessment--default.html` | change (delete) | The retired route no longer renders; the stale snapshot is removed so `Update-TestUiSnapshots.ps1 -Verify` and `Test-UiCatalogue.ps1` stay green. | Snapshot tooling (UIIMP-005/UIIMP-013). |
| `tests/Pegasus.IntegrationTests/AssessmentCopyWebTests.cs` | change | Retarget moved content assertions and add the exact 301 + `Location` assertion. | Existing permanent-redirect test convention (`MovedPermanently` + `Headers.Location`). |
| `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` | change | Post estimate operations to the new Case handler host and preserve whole-page import coverage. | Existing ENG-028 import cases. |
| `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs` | change | Assert Case-section projection rather than a 200 Assessment page. | Existing workspace test data. |
| `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs` | change | Move browser assertions to `?section=estimate` and the Case frame. | Existing browser fixture/support. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | change | Retarget report-draft POST coverage to the Case handler host. | Existing report draft fixtures. |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | change | Retarget Send to Claude web calls without changing the AI-job contract. | Existing `ICreateAiJob` integration coverage. |
| `tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs` | create | Prove all five section IDs render in every lifecycle state and Complete is read-only. | Existing Case and Assessment workspace fakes. |

## Must not touch (another EPIC-012 lane owns them)

- `src/Pegasus.Web/Pages/Cases/Details.cshtml`, `Details.cshtml.cs` and
  `Shared/_CaseWorkspaceNav.cshtml` — CASE-038 (frame, section containers,
  handler host, removal of the "Open Assessment" action at `Details.cshtml`
  line 276, and the matching `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
  assertion).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml` — CASE-029.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEngineerNotes.cshtml` — CASE-039.
- Sign-off Engineer, Inspect-at and Awaiting-instruction Case UI files —
  CASE-040, CASE-041 and CASE-042 respectively.
- Damage-map assets/handlers — ENG-036; report-image preparation
  assets/handlers — ENG-031; Settlement and Report field editors — ENG-029;
  fee-note preview — DOCS-018.
- `src/Pegasus.Core/Assessment/**` vocabulary/report-projection changes,
  `src/Pegasus.Infrastructure/Persistence/**` and
  `src/Pegasus.Infrastructure/Persistence/Migrations/**` — ENG-035 and the
  serialized migration lane.
- `wwwroot/css/site.css`, `wwwroot/js/site.js` and frame CSS/JS — CASE-038.
- `docs/design/test-ui/**` beyond the two rows above (the new Case-record
  snapshot states and their catalogue entries) — UIIMP-014.
- `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` line 427 ("Review
  estimate" links to `/Cases/Assessment/Index`) and
  `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` line 235 — Operations
  lane; the 301 keeps that link working, so no change is required here.
- Governing documentation — DELIV-041.
