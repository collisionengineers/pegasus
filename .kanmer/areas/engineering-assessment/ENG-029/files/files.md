# Files — ENG-029 (2026-09-02)

Produced by gpt-5.6-terra (high) in the read-only `.worktrees/research`
checkout at `897db953`; path existence and the sibling-lane ownership claims
below were re-checked by the Claude wrapper against the repo and the board
files (`areas/*/<id>/files/files.md`).

## Planned files

**ASSUMED** — this is the smallest ENG-029 change set after ENG-034, ENG-035,
PLAT-068, CASE-040, DOCS-017, and the CASE-038 handler-host hand-off land.
Every existing path was checked with `Test-Path` or `rg --files`; absent
Settlement/Report partials were confirmed with `Get-ChildItem`.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` | change | Replace ENG-034's read-only shell with Settlement controls and derived values. | `AssessmentVocabulary`, `EstimateTotals`, Case partial panels, `OperatorLabels`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` | change | Replace ENG-034's read-only shell with Report fields, named readiness, and draft actions. | `AssessmentReportProjection.Prepare`, Case partial forms, `OperatorLabels`. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change, only after explicit CASE-038/CASE-040 whole-file hand-off | Host the Case-page assessment save handler and load data required by the two partials. | Former `OnPostSaveDamageAsync` (`36655f26^`), `CaseMutationPageModel`, `ISaveAssessment`. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change, serialized shared lock | Add only ENG-029's Settlement and Report editor vocabulary. | `OperatorLabels.CaseWorkspace`. |
| `tests/Pegasus.IntegrationTests/CaseAssessmentEditorsWebTests.cs` | create | Prove Case-page posts round-trip supported fields, preserve lease/version behaviour, show named readiness, and pass saved values to preview. | Assessment persistence and report-draft web test fixtures. |

**VERIFIED** — `rg -n 'CK_CaseAssessmentFields_FieldPath'
src/Pegasus.Infrastructure/Persistence/Migrations/
PegasusDbContextModelSnapshot.cs` — no migration belongs in this ticket.
ENG-035 changes the vocabulary and constraint before these editors bind its
new D41 fields.

**VERIFIED** — `rg -n 'Update-TestUiSnapshots|Test-UiCatalogue'
scripts/*.ps1` and the live board ticket `UIIMP-014` — ENG-029 must not edit
`docs/design/test-ui/**`; UIIMP-014 owns new snapshot states and catalogue
entries.

## Must not touch

- **VERIFIED** — `ENG-034 files/files.md` — ENG-034 owns
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml`,
  `Index.cshtml.cs`, `Suggestions.cshtml`, `_CaseDamage.cshtml`,
  `_CaseEstimate.cshtml`, route-retirement tests, and the old Assessment
  catalogue reclassification. ENG-029 changes only the bodies of
  `_CaseSettlement.cshtml` and `_CaseReport.cshtml` after their creation.

- **VERIFIED** — `ENG-035 files/files.md` — ENG-035 owns
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
  `AssessmentPolicy.cs`, `src/Pegasus.Infrastructure/Persistence/
  EfCaseAssessmentStore.cs`, `src/Pegasus.Core/Reports/
  AssessmentReportProjection.cs`, `AssessmentReportRendering.cs`,
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
  `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
  and all `Persistence/Migrations/**` changes.

- **VERIFIED** — `PLAT-068 files/files.md` — PLAT-068 owns
  `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`,
  `EfStaffAccountQueries.cs`, `PegasusDbContext.cs`, Administration Accounts
  pages, and the staff-account sign-off migration.

- **VERIFIED** — `CASE-040 files/files.md` — CASE-040 owns
  sign-off Case identity and defaulting in `CaseWorkflowContracts.cs`,
  `CaseLifecycle.cs`, workflow persistence, `Workflow.cshtml.cs`,
  `Shared/_CaseSummary.cshtml`, and `Cases/Eva/Send.*`. Do not change
  `Details.cshtml.cs` unless CASE-038 and CASE-040 transfer its whole-file
  ownership for the assessment-handler addition.

- **VERIFIED** — `ENG-031 files/files.md` — ENG-031 owns
  `ReportImageCuration.cs`, `_CaseReportImages.cshtml`, `cropper.js`,
  report-image persistence, and report-image readiness/projection changes.

- **VERIFIED** — `ENG-036` ticket and `rg --files src/Pegasus.Web |
  rg 'damage-diagram'` — ENG-036 owns the presently absent damage diagram,
  damage controls, styles, JavaScript, and diagram-report output.

- **VERIFIED** — `CASE-029 files/files.md` — CASE-029 owns
  `_CaseVehicle.cshtml`, `_CaseValuation.cshtml`, `Cases/Vehicle.cshtml.cs`,
  `Cases/Valuation.*`, valuation persistence, and its migration.

- **VERIFIED** — `DOCS-018` ticket — DOCS-018 owns the Report-section fee
  note preview. ENG-029 edits agreed-fee inputs only.

- **VERIFIED** — `UIIMP-014` ticket and `rg -n 'catalogue|prototype'
  scripts/Test-UiCatalogue.ps1` — UIIMP-014 owns
  `docs/design/test-ui/**` new Case-record snapshots and browser walk.
