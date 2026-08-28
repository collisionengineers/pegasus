# Files — PLAT-029

## Owned (modified / created / deleted)

- `src/Pegasus.Web/wwwroot/css/site.css` (replaced)
- `src/Pegasus.Web/wwwroot/js/site.js` (new delimited sections)
- `src/Pegasus.Web/wwwroot/fonts/inter/{InterVariable.woff2,InterVariable-Italic.woff2,LICENSE.txt}` (new)
- `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` (grown)
- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml`, `_LayoutAuth.cshtml`, `_LayoutExternal.cshtml`, `_LucideSprite.cshtml`, `_PageHeader.cshtml`, `_MetricCard.cshtml`, `_StatusChip.cshtml`, `_FreshnessBanner.cshtml`, `_ReasonDialog.cshtml`, `_EvidenceViewer.cshtml`, `_ImageGallery.cshtml`, `_UploadOutcome.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml` (new)
- `src/Pegasus.Web/Pages/Administration/Shared/_AdminNav.cshtml` (new), `Pages/Administration/Index.cshtml(.cs)`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs`, `Presentation/RailCountsPageFilter.cs`
- Routes: `Pages/Search/Index.*` (was stub; becomes moved Cases search), `Pages/Cases/Index.*` (becomes moved Triage queues), `Pages/Triage/Index.*` (new 301 stub), `Pages/Unidentified/Index.cshtml.cs`, delete `Pages/ImageIntake/Index.*`
- Auth family: `Pages/Account/SignIn.cshtml`, `AccessDenied.cshtml`, `PasswordChange.cshtml`, `Pages/Error.cshtml`, `Pages/StatusCode.cshtml`, `Pages/Connect/Authorize.cshtml`
- Tests: `RailCountsWebTests.cs`, `ShellAndStatusPageWebTests.cs`, `Browser/AccessibilityTests.cs`, new `Browser/LayoutIntegrityTests.cs`, class/route touch-ups in `Browser/OperatorJourneyTests.cs`, `Browser/UploadDropzoneBrowserTests.cs`, `Browser/UploadCaseSearchBrowserTests.cs`, `Browser/AssessmentReadinessSummaryBrowserTests.cs`, `TriageQueuesWebTests.cs`, `ImageIntakeWebTests.cs`, `AdministrationSearchAccountWebTests.cs`
- `docs/design/test-ui/catalogue.json`, `docs/design/test-ui/index.html` (links), delete `docs/design/test-ui/pages/vehicle-images--{default,empty}.html`

## Inbound-reference fixes (one-line link edits only)

- `Pages/Index.cshtml` (metric hrefs `/Triage?queue=` → `/Cases?tab=`)
- `Pages/ImageIntake/Details.cshtml` (back link), `Pages/Triage/Details.cshtml` (crumb), `Pages/Cases/Details.cshtml` (crumb), `Presentation/UploadOutcome.cs` (no list link found — detail link stays)

## Consumed, not modified

- `Program.cs` (RailCountsPageFilter registration, CSP, 404 middleware)
- `Pages/StaffPageModel.cs`, `Core/Actors/StaffSessionPolicy.cs`, `Core/Identity/StaffRoleNames`
- `scripts/Test-UiCatalogue.ps1`, `tests/.../Browser/BrowserTestSupport.cs`

## Added by review of PR #589 (2026-08-28)

- `src/Pegasus.Core/Operations/DashboardCounts.cs` — `CaseStageCounts` gains
  `WithEngineer` (ReportPreparation + PostReport); small Core addition
  allowed for this ticket by the reviewer.
- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` — the stage
  aggregate includes the two With-Engineer states.
- Constructor-arity touch-ups: `Pages/Index.cshtml.cs`,
  `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`,
  `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`.
- `docs/design/README.md` — D13 font rows, sprite checksum line and the
  43 glyph checksums (added to Owns by the reviewer).
- `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` — the
  page-rendered reason-dialog focus/click assertion.
