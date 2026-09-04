# Post-implementation report — ENG-034 (2026-09-04)

Implementer: gpt-5.6-sol (high) under a Sonnet wrapper. PR:
https://github.com/collisionengineers/pegasus/pull/668
(`task/eng-034-engineer-sections-move` → `dev`).

## What changed

### Application code

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` (create/replace
  CASE-038 shell) — read-only Damage section: impact location, impact
  severity, incident narrative, using `AssessmentVocabulary` scalars and the
  `_CaseVehicle`/`_CaseSummary` `Value(...)` display convention. No damage
  type (D45).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` (create/replace
  CASE-038 shell) — moved the ENG-028 estimate tabs, selected editor, lines,
  totals, import control/dialog, discard dialog and Send to Claude entry and
  dialog verbatim from `Assessment/Index.cshtml`, now posting to the Case
  handler host.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` (create/replace
  CASE-038 shell) — read-only outcome/salvage/cost values.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` (create/replace
  CASE-038 shell) — read-only Engineer's comments, history check, agreed fee,
  fee description lines, statement of truth, plus the moved
  Generate/Preview report-draft controls. No signatory tuple (D31), image
  curation, crop entry (D46) or fee-note preview (D42) — those are later
  tickets' scope.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` (change, capacity-one
  lease) — received the moved Assessment handler surface: lease
  claim/heartbeat/release, `SaveEstimate`, `EditLine`, `DuplicateEstimate`,
  `DiscardEstimate`, `SetCurrentEstimate`, `ImportEstimate`, `SendToClaude`,
  `GenerateReportDraft`, `PreviewReportDraft`. Mutating results redirect to
  `/Cases/{id}?section=estimate`.
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` /
  `Index.cshtml.cs` (change) — reduced to the authorised redirect-stub shape;
  `OnGet` returns `RedirectPermanent("/Cases/{id}?section=estimate")`. No
  handler surface remains.
- `src/Pegasus.Web/Pages/Cases/Assessment/Suggestions.cshtml` (change) —
  Back link retargeted to the Case Estimate section.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` (change) — added one
  comment-delimited `// ENG-034: ... // ENG-034 end.` block under
  `CaseWorkspace.EngineerSections` (lines 1453–1570) carrying every
  operator-visible and accessible-name literal moved with the Estimate,
  Damage, Settlement and Report markup, at unchanged wording. Reused
  `OperatorLabels.CaseStage`, `RepairSpecificationRoute`, `EstimateLineType`
  and the existing `AssessmentVocabulary` scalar vocabulary paths rather than
  adding a parallel label/state map.
- `Details.cshtml` needed no edit: CASE-038 had already landed all four
  section include points.

### Test UI catalogue and snapshots

- `docs/design/test-ui/catalogue.json` — `Pages/Cases/Assessment/Index.cshtml`
  reclassified `visual` → `redirect` with a D30/ENG-034 reason.
- `docs/design/test-ui/pages/case-assessment--default.html` — deleted (stale
  snapshot for the retired route).
- `docs/design/test-ui/pages/case-details--default.html` and
  `case-details--conflict.html` — regenerated (Engineer sections now render
  real content instead of CASE-038's heading-only shells).
  `case-details--unavailable.html` was regenerated and inspected but is
  byte-identical to the committed version (no Engineer-section markers
  render when the Case query fails, as expected).
- `docs/design/test-ui/index.html` — the one `/Cases/{id:guid}/Assessment`
  row moved from the visual-routes list to the non-visual-routes table
  (mechanical fix required by `Test-UiCatalogue.ps1` after the snapshot
  deletion above; see Deviations in `plan.md`).

### Tests

- `tests/Pegasus.IntegrationTests/AssessmentCopyWebTests.cs`,
  `AssessmentEstimateImportWebTests.cs`, `AssessmentVehiclePrefillWebTests.cs`,
  `Browser/AssessmentReadinessSummaryBrowserTests.cs`,
  `Reports/AssessmentReportDraftWebTests.cs`, `SendToAiIntegrationTests.cs` —
  retargeted GETs/forms/antiforgery reads/assertions to the Case handler
  host; every existing behavioural assertion (totals, import, duplicate,
  discard, current-estimate, report-draft, preview, Send to Claude) is
  unchanged; added the exact
  `HttpStatusCode.MovedPermanently` + `Location: /Cases/{id}?section=estimate`
  assertion for the retired route.
- `tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs` (new) —
  proves all Engineer section IDs render in every Case lifecycle state,
  Complete renders recorded values with mutation controls absent (not
  disabled), and no staff-review wording (D44) or damage type (D45) appears
  anywhere.

## Deviations from the plan

Recorded in full, with reasoning, in `plan.md` under "Deviations recorded
during implementation (2026-09-04)": the mechanical `index.html` fix, the
confirmed `/Cases/{id}?section=<key>` redirect/jump target (rather than a
`/Cases/{id}/Section` fragment form), proceeding with implementation ahead
of CASE-039's merge per the parallel-build policy (merge order is the
controller's to set, not build order), leaving pre-existing host parameter
names unchanged, and `Details.cshtml` needing no edit.

## Simplification pass

Recorded in `plan.md` under "Simplification pass (2026-09-04)": one reuse
fix (a shared `AssessmentValue` display helper, removed a duplicate
`NotRecorded` label), one efficiency fix (materializing posted line-field
collections once instead of per loop iteration in `ReadEditorPost`); no
simplification or altitude findings; no behavioural bugs found. Every
verification command below was rerun green after applying these fixes.

## Commands run and results (all quoted, all rerun after the simplification pass)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | Locked restore passed. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,225 passed. |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentCopyWebTests\|FullyQualifiedName~AssessmentEstimateImportWebTests\|FullyQualifiedName~AssessmentVehiclePrefillWebTests\|FullyQualifiedName~AssessmentReportDraftWebTests\|FullyQualifiedName~SendToAiIntegrationTests\|FullyQualifiedName~CaseEngineerSectionsWebTests"` | 0 | 36 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentReadinessSummaryBrowserTests" -- xUnit.MaxParallelThreads=2` | 0 | 1 passed. |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~CaseEngineerSectionsWebTests\|FullyQualifiedName~AssessmentCopyWebTests\|FullyQualifiedName~TestUiFocusedRenderTests"` | 0 | Scoped capture; 84 capture-support tests + 1 snapshot-update test passed. |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` | 0 | Scoped verify, 1 test passed. |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 | 54 routed sources, 58 prototypes, 0 broken local references. |

Non-browser and browser test commands ran as the controller-instructed
"Core + Architecture + only the changed integration/browser classes"
profile, not the full `Category!=Corpus&Category!=Browser` / `Category=
Browser&Category!=Corpus` solution-wide profiles the plan's Commands section
names (per this repository's stated local-checks policy, which defers the
full/solution-wide profiles to CI). `./scripts/Test-MigrationGrants.ps1` is
not applicable: this ticket adds no migration.

## Snapshot artifact facts (opened and inspected)

- `case-details--default.html`: 66,113 bytes; begins `<!DOCTYPE html>`;
  `class="case-sticky"` present; 11 distinct `id="section-*"` hosts (damage,
  engineer-notes, estimate, files, inspection, notes, overview, report,
  settlement, valuation, vehicle); no `<img src="#">`.
- `case-details--conflict.html`: 41,777 bytes; same doctype/marker profile.
- `case-details--unavailable.html`: 24,390 bytes; doctype present; Engineer
  section markers correctly absent (Case query failed, unavailable notice
  renders instead); no content diff from the committed version.
- Retired `case-assessment--default.html`: deleted, confirmed absent.

## Board dependency status confirmed before starting

- CASE-038: merged to `dev` (PR #656, commit `ddbbc5e8c`).
- ENG-035: `done`, merged to `dev` (PR #648).
- PLAT-070: `done`, merged to `dev` (PR #649) — D44 staff-review flags/wording
  removed from the host before this ticket started.
- CASE-039: still `implementing` at PR-open time (also touches
  `Details.cshtml`/`Details.cshtml.cs` under the same capacity-one lock).
  Per this repository's parallel-build policy, "serialized" plan text sets
  merge order, which the reviewing controller orders; it is not a reason to
  wait idle on the build. No merge was performed by this lane.

## PR

https://github.com/collisionengineers/pegasus/pull/668
