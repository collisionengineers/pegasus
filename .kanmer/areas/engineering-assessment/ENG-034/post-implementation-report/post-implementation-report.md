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

## Review round fixes (2026-09-05)

Codex (`gpt-5.6-sol`, high) was dispatched for this round but hit its usage
limit before making any change (`ERROR: You've hit your usage limit ...`,
`CODEX_EXIT=0` from the wrapper, no working-tree diff, no output file). The
fixes below were implemented directly by the Claude wrapper session in the
same worktree/branch instead.

Commit `bd032ceb7cf0df5172de9a4c8940e08713034cbd`, pushed to
`origin/task/eng-034-engineer-sections-move`.

### BLOCKER — restored the CanOpen mutation gate

- `GuardEstimateEditAsync` (`Details.cshtml.cs`) and `OnPostImportEstimateAsync`
  restored to the pre-move check `access?.CanOpen != true → NotFound()`
  (previously relaxed to a bare `access is null` check), matching the guard
  removed commit `99c27e906`'s move dropped. This one shared guard covers
  `SaveEstimate`, `EditLine`, `DuplicateEstimate`, `DiscardEstimate`,
  `SetCurrentEstimate`; `ImportEstimate` has its own identical check.
  `OnPostSendToClaudeAsync`/`HasAssessmentAccessAsync` was NOT touched —
  not named in the finding, out of this ticket's scope.
- Added `DetailsModel.AssessmentCanOpen` (populated in `OnGetAsync` from the
  same `getAssessmentAccess` call already made for `AssessmentIsReadOnly`,
  no second query added; fails closed to `false` on unresolved access).
- `_CaseEstimate.cshtml`: the "New estimate" link and `canImport` (and its
  gated/disabled fallback) now also require `AssessmentCanOpen`, so those
  controls are absent (not disabled) when the workspace can't open — D30's
  section-visibility rule is untouched (the five sections themselves still
  always render).
- `SelectedEstimateIsEditable`, `SelectedEstimateCanBeDuplicated`,
  `SelectedEstimateCanBeCurrent` now also require `AssessmentCanOpen`, so an
  already-open estimate editor/Duplicate/Use-estimate/Delete-estimate
  control never renders in a state where the restored backend guard would
  just 404 it.
- Retargeted (not re-created as a new file) the deleted
  `InaccessibleCaseCannotPostAssessmentChanges` test as
  `InaccessibleCaseCannotPostEstimateMutations` in the existing
  `AssessmentCopyWebTests.cs` (that file is itself the pre-move
  `AssessmentCopyWebTests.cs`'s retarget onto the Case handler host): posts
  to `?handler=SaveEstimate&section=estimate` with `canOpen: false` and
  asserts `404 Not Found`, proving the restored gate through the real HTTP
  pipeline. Updated the class's summary doc comment to mention it.

### SHOULD-FIX — restored the negative assertion

`AssessmentVehiclePrefillWebTests.ExtractedVehicleFactsTakePrecedenceOverLookupObservation`
now also asserts `Assert.DoesNotContain("VOLKSWAGEN", ...)` and
`Assert.DoesNotContain("GOLF", ...)` (the fixture's lookup make/model),
restoring the precedence proof the move had reduced to a presence-only
check.

### NITS

- Restored, verbatim from the pre-move blob
  (`99c27e906a9ed10d0d6c3636e001e1dfa245bfed^:src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`),
  the XML doc comments above `OnPostSaveEstimateAsync`, `OnPostEditLineAsync`,
  `OnPostDuplicateEstimateAsync`, `OnPostDiscardEstimateAsync`,
  `OnPostSetCurrentEstimateAsync`, `OnPostImportEstimateAsync`, and the
  inline "Carried forward only while the line still has no price..." comment
  in `OnPostSaveEstimateAsync`'s line-projection.
- `OperatorLabels.CaseWorkspace.EngineerSections.SpecificationLinesCaption`'s
  straight apostrophe: left as-is. The review finding itself dispositioned
  this as accepted risk (cosmetic); no change made.
- Snapshot byte sizes restated at this reviewed head (unchanged by this
  round's diff, listed here per the finding): `case-details--default.html`
  69,470 bytes; `case-details--conflict.html` 42,707 bytes;
  `case-details--unavailable.html` 24,390 bytes (all confirmed by `wc -c`
  before making any change in this round).

### Snapshot recapture — not needed

Checked whether any committed Case Details snapshot page renders in a
`CanOpen: false` state before touching anything: neither
`case-details--default.html` nor `case-details--conflict.html` contains the
strings "New estimate" or "Import estimate" at all (`grep -c` returns 0 for
both, before and after this round's diff) — both captures render as a
non-Engineer actor, so those controls were already absent for a reason
unrelated to `CanOpen` (`ActorIsEngineer` gates them too). `AssessmentCanOpen`
being newly ANDed onto already-false conditions changes nothing about their
rendered bytes. `case-details--unavailable.html` renders on a failed Case
query, before any Engineer-section markup is reached, so it's unaffected by
definition. No Test UI snapshot recapture was run for this round.

### Verification run this round (worktree `.worktrees/eng-034`)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | Locked restore passed. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,240 passed. |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentCopyWebTests\|FullyQualifiedName~AssessmentEstimateImportWebTests\|FullyQualifiedName~AssessmentVehiclePrefillWebTests\|FullyQualifiedName~CaseEngineerSectionsWebTests\|FullyQualifiedName~AssessmentReportDraftWebTests\|FullyQualifiedName~SendToAiIntegrationTests"` | 0 | 37 passed (36 pre-existing + the retargeted `InaccessibleCaseCannotPostEstimateMutations`). |

No test was weakened or deleted. Not merged; PR #668 remains open for the
epic owner/review controller to re-review and merge.

## Review round fixes (2026-09-05)

Second round on the same PR (#668), same branch/worktree
(`task/eng-034-engineer-sections-move`, `.worktrees/eng-034`), addressing
the remaining findings from the review that were not covered by the first
`Review round fixes (2026-09-05)` section above (commit `bd032ceb7`).

Codex (`gpt-5.6-sol`, high) was dispatched for this round but hit its usage
limit before making any change (`ERROR: You've hit your usage limit ...`,
`CODEX_EXIT=0` from the wrapper, no working-tree diff). The fixes below were
implemented directly by the Claude wrapper session in the same
worktree/branch instead.

Commit `6a2c3af779201144def500c964524902fc560d79`, pushed to
`origin/task/eng-034-engineer-sections-move`.

### BLOCKER — restored CanOpen on HasAssessmentAccessAsync

`HasAssessmentAccessAsync` (`Details.cshtml.cs`, the sole caller being
`OnPostSendToClaudeAsync`) still checked `is not null` after the first
round's fix, which only touched `GuardEstimateEditAsync` and
`OnPostImportEstimateAsync`. Confirmed by reading pre-move
`99c27e906^:src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:1372-1378`
that the original `CanAccessAsync` required `?.CanOpen == true`. Changed
`HasAssessmentAccessAsync` to `(await getAssessmentAccess.ExecuteAsync(...))
?.CanOpen == true`, so `OnPostSendToClaudeAsync` returns 404 again for a
case whose assessment workspace has not opened (D11), closing the gap D30's
removed page-level 404 used to cover. Confirmed `HasAssessmentAccessAsync`
has exactly one caller before changing it — no other behaviour depends on
the looser "record exists" semantics.

### Missing test coverage — added

`SendToAiIntegrationTests.Compose` gained a `canOpen: bool = true` parameter
threaded into `new FakeGetAssessmentAccess(canOpen)` (mirroring the same
parameter already on `AssessmentCopyWebTests.Compose`). Added
`InaccessibleCaseCannotPostSendToClaude`: composes with `canOpen: false`,
confirms the Send to Claude control renders gated/disabled (same regex
assertion shape as the existing `ASwitchedOffControlStatesTheConditionAndIsNotOffered`),
then POSTs to `/Cases/{id}?handler=SendToClaude&section=estimate` and
asserts `404 Not Found`. No existing assertion in the file was weakened.

### Absent, not a dead end — SendToClaudeCondition now considers CanOpen

Added an `!AssessmentCanOpen` branch to `EvaluateEngineerSectionConditionsAsync`
(checked right after `AssessmentIsReadOnly`, before the AI-toggle and
Engineer's-Value checks), setting `SendToClaudeCondition` to the existing
`Labels.CaseWorkspace.EngineerSections.NotAvailableForCase` label — reused
rather than adding a new one, since it already carries the right generic
"not available for this case" meaning and is already used for
`ReportDraftCondition`'s equivalent not-ready case. No new label was added
to `OperatorLabels.cs`. `_CaseEstimate.cshtml` needed no edit: both the
button-row entry (`Model.SendToClaudeCondition is null`) and the dialog
guard (`!Model.AssessmentIsReadOnly && Model.SendToClaudeCondition is
null`) already key off `SendToClaudeCondition`, so the control now renders
gated/disabled instead of a clickable dead end whenever `CanOpen` is false.

### NIT — byte-count discrepancy: not reproduced at this head

Checked `wc -c docs/design/test-ui/pages/case-details--unavailable.html`:
24,390 bytes, matching the previous round's report entry exactly. No
24,694-byte state was found in the current worktree or in the file as
committed on this branch; no snapshot regeneration was needed or run (no
routed Razor page, partial, or `catalogue.json` changed this round).

### Verification run this round (worktree `.worktrees/eng-034`)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SendToAiIntegrationTests\|FullyQualifiedName~AssessmentCopyWebTests"` | 0 | 11 passed (includes the new `InaccessibleCaseCannotPostSendToClaude`). |

No test was weakened or deleted. Not merged; PR #668 remains open for the
epic owner/review controller to re-review and merge.
