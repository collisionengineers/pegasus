# Files — UIIMP-014 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

Wrapper note (Claude): every current path below was confirmed to exist with
`ls`/`grep` in the main checkout; the `pages/*.html` rows marked "proposed"
are future generated files whose exact names follow the merged section keys
(D30 order) and the verified flat-file rule `pages/<route>--<state>.html`.
`case-details--default.html` and `case-details--conflict.html` are
CASE-038's reserved regenerations and are not listed here.

## UIIMP-014-owned changes after sibling lanes merge

The final filenames below are proposed catalogue names, not current facts.
They follow the verified flat-file validator. The merged Case implementation
must determine the final section keys; do not create a parallel naming scheme.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | change | Add explicit matchers for all new Case-section/mode, Awaiting instruction, and Operations notice scenarios; remove the retired Assessment visual scenario. | `StateMatches`, `Generate` |
| `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | change | Render and capture deterministic server-side states for the redirect, queue, and notice where browser journeys do not already produce them. | `IntakeWebApplicationFactory`, focused Razor pattern |
| `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | change | Add the seeded Case record walk at 1580/1100/760 and inspect every section plus edit mode. | Existing geometry assertions |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | change only if extraction is necessary | Expose/reuse its seeded accepted-Case and lease pattern for the layout walk. Keep it unchanged if a small local helper is sufficient. | `SeedCustodyRecoveryCaseAsync`, `RepositoryEvaFixture`, lease flow |
| `docs/design/test-ui/catalogue.json` | change | Add visual state records, convert Assessment to `redirect` with reason, and remove its visual state. | Existing manifest schema |
| `docs/design/test-ui/index.html` | change (generated) | Generated route/state index after catalogue changes. | `BuildIndex` |
| `docs/design/test-ui/pages/case-details--overview-read-only.html` | create (proposed) | Overview read-only snapshot. | Generated Razor response |
| `docs/design/test-ui/pages/case-details--overview-edit.html` | create (proposed) | Overview edit snapshot. | Generated Razor response |
| `docs/design/test-ui/pages/case-details--engineer-notes-read-only.html` | create (proposed) | Engineer notes read-only snapshot. | CASE-039 rendered section |
| `docs/design/test-ui/pages/case-details--engineer-notes-edit.html` | create (proposed) | Engineer notes edit snapshot. | CASE-039 rendered section |
| `docs/design/test-ui/pages/case-details--inspection-read-only.html` | create (proposed) | Inspection read-only snapshot. | CASE-041 rendered section |
| `docs/design/test-ui/pages/case-details--inspection-edit.html` | create (proposed) | Inspection edit snapshot. | CASE-041 rendered section |
| `docs/design/test-ui/pages/case-details--vehicle-read-only.html` | create (proposed) | Vehicle read-only snapshot. | CASE-029 rendered section |
| `docs/design/test-ui/pages/case-details--vehicle-edit.html` | create (proposed) | Vehicle edit snapshot. | CASE-029 rendered section |
| `docs/design/test-ui/pages/case-details--damage-read-only.html` | create (proposed) | Damage read-only snapshot. | ENG-036 rendered section |
| `docs/design/test-ui/pages/case-details--damage-edit.html` | create (proposed) | Damage edit snapshot. | ENG-036 rendered section |
| `docs/design/test-ui/pages/case-details--valuation-read-only.html` | create (proposed) | Valuation read-only snapshot. | CASE-029/AUTO-018 rendered section |
| `docs/design/test-ui/pages/case-details--valuation-edit.html` | create (proposed) | Valuation edit snapshot. | CASE-029/AUTO-018 rendered section |
| `docs/design/test-ui/pages/case-details--estimate-read-only.html` | create (proposed) | Estimate read-only snapshot. | ENG-034/035 rendered section |
| `docs/design/test-ui/pages/case-details--estimate-edit.html` | create (proposed) | Estimate edit snapshot. | ENG-034/035 rendered section |
| `docs/design/test-ui/pages/case-details--settlement-read-only.html` | create (proposed) | Settlement read-only snapshot. | ENG-029 rendered section |
| `docs/design/test-ui/pages/case-details--settlement-edit.html` | create (proposed) | Settlement edit snapshot. | ENG-029 rendered section |
| `docs/design/test-ui/pages/case-details--report-read-only.html` | create (proposed) | Report/fee-note read-only snapshot. | ENG-034, DOCS-018 |
| `docs/design/test-ui/pages/case-details--report-edit.html` | create (proposed) | Report edit snapshot. | ENG-034, DOCS-018 |
| `docs/design/test-ui/pages/case-details--files-read-only.html` | create (proposed) | Files read-only snapshot. | Existing `_CaseFiles`, ENG-031 |
| `docs/design/test-ui/pages/case-details--files-edit.html` | create (proposed) | Files edit snapshot. | Existing `_CaseFiles`, ENG-031 |
| `docs/design/test-ui/pages/case-details--notes-read-only.html` | create (proposed) | Notes read-only snapshot. | Existing `_CaseHistory`/Notes implementation |
| `docs/design/test-ui/pages/case-details--notes-edit.html` | create (proposed) | Notes edit snapshot. | Existing Notes implementation |
| `docs/design/test-ui/pages/queues--awaiting-instruction.html` | create (proposed) | Awaiting instruction pre-case queue state on `/Cases` (the Cases queue page's snapshots are the `queues--*` files; `cases--*` belong to Search). | CASE-042 response |
| `docs/design/test-ui/pages/operations--partial-data.html` | create (proposed) | Operations one-line partial-data notice. | PLAT-069 response |
| `docs/design/test-ui/pages/case-assessment--default.html` | change (remove obsolete generated page) | Assessment becomes a catalogue `redirect`, so no visual snapshot may remain. | `TestUiSnapshotTests` orphan check |

`TestUiResponseCapture.cs`, `BrowserTestSupport.cs`,
`Update-TestUiSnapshots.ps1`, and `Test-UiCatalogue.ps1` should remain
unchanged unless the landed implementation demonstrates a concrete missing
capture capability or catalogue rule. The current machinery already supports
the required classifications, capture, generated index, and three-width
browser setup.

## Files UIIMP-014 must not touch

These lane boundaries were read from the board by the wrapper (EPIC-012
members and CASE-038's file map) and quoted to Codex.

- CASE-038: `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
  `Details.cshtml.cs`, `Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`,
  `src/Pegasus.Web/wwwroot/css/site.css`, `src/Pegasus.Web/wwwroot/js/site.js`,
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`, and
  `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`. Also do not
  regenerate its reserved `case-details--default.html` or
  `case-details--conflict.html`.
- ENG-034: `src/Pegasus.Web/Pages/Cases/Assessment/**` and Engineer partials
  `_CaseDamage`, `_CaseEstimate`, `_CaseSettlement`, `_CaseReport`; it owns the
  actual 301 redirect.
- ENG-035: `src/Pegasus.Core/Assessment/**`,
  `src/Pegasus.Infrastructure/Persistence/**`, and
  `src/Pegasus.Infrastructure/Persistence/Migrations/**`.
- CASE-039: `_CaseEngineerNotes`.
- CASE-040: `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` and `.cshtml.cs`.
- CASE-041: `_CaseInspectionAddress` and inspection-resolution ports.
- CASE-042: `src/Pegasus.Web/Pages/Cases/Index.cshtml` and `.cshtml.cs`.
- CASE-029: `_CaseVehicle`, `_CaseValuation`, `Vehicle.*`, and `Custody.*`.
- CASE-009: Case query-email implementation files.
- AUTO-018: AI market-research job implementation files.
- DOCS-017: report signatory policy documents.
- DOCS-018: Report-section fee-note implementation.
- ENG-029: Settlement and Report editors.
- ENG-031: case-evidence image preparation and selection.
- ENG-036: vehicle damage-map implementation.
- PLAT-068: Administration account setting files.
- PLAT-069: `src/Pegasus.Web/Pages/Operations/**` and Administration service
  health files.
- DELIV-030: `docs/current-architecture.md` and `docs/operations.md`.
- UIIMP-010: final independent browser-walk proof record.
- `docs/operator-notes.md` (protected) and `corpus/` (local, immutable) are
  never touched.
