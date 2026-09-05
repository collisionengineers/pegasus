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

## Review round fixes (2026-09-05, round 3)

Third round on the same PR (#668), same branch/worktree
(`task/eng-034-engineer-sections-move`, `.worktrees/eng-034`), addressing the
round-3 review findings (CI green at `6a2c3af77`). Codex was unavailable this
round (reported unavailable before dispatch); the fix below was implemented
directly by the Claude wrapper session in the same worktree/branch instead.

Commit `795506d752bb0ce9e1e82bbee06678b412f8884f`, pushed to
`origin/task/eng-034-engineer-sections-move`.

### SHOULD-FIX — ReportDraftCondition now considers AssessmentCanOpen

`ReportDraftCondition` (`Details.cshtml.cs`,
`EvaluateEngineerSectionConditionsAsync`) ignored `AssessmentCanOpen`, so
`_CaseReport.cshtml` rendered an active "Generate report draft" form and
"Preview report draft" link for a case whose assessment workspace has not
opened; both 404 at `AssessmentReportProjection.cs:434` — the same
`CanOpen`-gated dead end the two earlier review rounds closed for the
Estimate mutation handlers and Send to Claude. Confirmed the mechanism: the
Case page always calls `getAssessmentWorkspace.ExecuteAsync`, which is not
itself gated by `CanOpen` (`AssessmentWorkspace.cs`), so `ReportDraftPreparation`
can be non-null (and `CanGenerate: true`) even when `AssessmentCanOpen` is
false — exactly the case the existing
`AssessmentReportDraftWebTests.CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly`
test exercises (it composes with `canOpen: false` but a fully-populated,
generate-ready projection).

Fix: `ReportDraftCondition` is now
`!AssessmentCanOpen || ReportDraftPreparation is null ? NotAvailableForCase :
...` — reusing the existing `EngineerSections.NotAvailableForCase` label (no
new label added; it is the same label already used for
`SendToClaudeCondition`'s equivalent not-open case). `_CaseReport.cshtml`
needed no edit: it already renders the gated/disabled span whenever
`ReportDraftCondition` is non-null.

Extended the existing
`CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly` test (not a
new test) to fetch the page HTML first (already fetched, previously unused
beyond the antiforgery token) and assert
`Assert.DoesNotContain("handler=\"GenerateReportDraft\"", html, ...)` and
`Assert.DoesNotContain("Preview report draft", html, ...)`, matching the
assertion shape already used by the sibling `NotReady` test
(`IncompleteCaseFailsClosedNamingWhatIsMissingInsteadOfThrowing`). No
existing assertion was weakened; the POST-then-404 assertion in the same
test is unchanged.

### NIT — case-details--unavailable.html byte count

Re-measured `docs/design/test-ui/pages/case-details--unavailable.html` at
this head with `wc -c` (Bash) and `Get-Item .Length` (PowerShell): both
report **24,390 bytes**, matching this report's existing entry exactly (the
round-2 section above already recorded this same 24,390 figure, confirmed
against `wc -c` before making any change). No 24,694-byte state exists in
the current worktree, the committed blob (`git cat-file -s`, also 24,390),
or the branch history for this file. Rejecting this nit as not reproduced at
the current head — no correction was made because the report already states
the measured value; disposition: **rejected, not reproduced** (the finding's
premise does not hold against this branch's actual committed bytes).

No snapshot recapture was run this round: no routed Razor page, partial it
composes, or `catalogue.json` changed (only the C# condition in
`Details.cshtml.cs` and a test assertion changed). Ran the scoped
`-Verify -SkipCapture -Scope case-details` to confirm per the round
instruction — it passed (not stale), consistent with the existing report
note that the captured default case has an open workspace and is unaffected
by this condition.

### Verification run this round (worktree `.worktrees/eng-034`)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | Locked restore passed. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,240 passed. |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentReportDraftWebTests"` | 0 | 4 passed (including the extended `CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly`). |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` | 0 | 1 test passed, not stale. |

No test was weakened or deleted. Not merged; PR #668 remains open for the
epic owner/review controller to re-review and merge.

## Review round fixes (2026-09-05, round 4)

Fourth round on the same PR (#668), same branch/worktree
(`task/eng-034-engineer-sections-move`, `.worktrees/eng-034`), addressing
finding F2 (SHOULD-FIX) returned from this round's review. Codex was
reported unavailable for this round; the fix below was implemented directly
by the Claude wrapper session in the same worktree/branch instead.

Commit `9f06b46a4cd6a636dd3aab035ca34ca80accbd92`, pushed to
`origin/task/eng-034-engineer-sections-move`.

### SHOULD-FIX (F2) — the test's fixture kind and its name had drifted apart

`AssessmentVehiclePrefillWebTests.cs:59` and `:161` composed
`FakeGetCase(caseId, includeExtractedFacts: true)`. The round-1/round-2 fix
above that restored this test's missing `DoesNotContain("VOLKSWAGEN"...)` /
`DoesNotContain("GOLF"...)` half also, in the same edit, changed the
fixture-helper it used from `Fact<T>` (`CaseDataValueKind.Fact`) to
`Confirmed<T>` (`CaseDataValueKind.Confirmed`) — necessarily, because
`_CaseVehicle.cshtml:44-46` reads `data?.Vehicle.Make.Confirmed?.Value` (and
the equivalent for Model and Mileage) only; a `Fact`-kind value renders "Not
recorded" and `Assert.Contains("FORD", html, ...)` would fail against it.
That swap was not itself wrong — the test has to feed the fixture that
`_CaseVehicle.cshtml` actually reads to pass against real behaviour — but it
changed which precedence tier the test proves (confirmed-over-lookup, not
extracted-fact-over-lookup) without the test's name, its parameter, or this
report saying so. It is the same defect the round-1 fix caught and corrected
in a different place (a weakened assertion, restored that round) landing
again here unnoticed, because the assertion *text* was never touched, only
its premise.

Fix: renamed the test
`ExtractedVehicleFactsTakePrecedenceOverLookupObservation` →
`ConfirmedVehicleFactsTakePrecedenceOverLookupObservation`, and the
`FakeGetCase` constructor parameter `includeExtractedFacts` →
`includeConfirmedFacts`, so the name and the fixture agree with what the
survived assertions actually exercise. No assertion, fixture value, or test
behaviour changed — this is a rename only. `_CaseVehicle.cshtml` is CASE-027's
file (confirmed with `git log --follow`) and is not named in ENG-034's
`files.md` as an owned or must-touch path, so no `src/` change was made and
no Test UI snapshot recapture was run (no routed Razor page, partial, or
`catalogue.json` changed).

### Report correction

The "What changed → Tests" section above states "every existing behavioural
assertion (totals, import, duplicate, discard, current-estimate,
report-draft, preview, Send to Claude) is unchanged" for the retargeted
Assessment test files. That sentence is corrected here: it is true of every
assertion's *wording*, but for
`ConfirmedVehicleFactsTakePrecedenceOverLookupObservation` the *fixture kind
behind* the surviving `Assert.Contains("FORD"/"FOCUS"/"40,000 miles", ...)`
assertions changed from `CaseDataValueKind.Fact` to
`CaseDataValueKind.Confirmed` during the round-1/round-2 fix, which is what
this round's rename now names accurately. No other retargeted test in that
list had its fixture kind, only its host/route, changed.

### Extracted-fact display gap — recorded, not fixed here

The retired `Assessment/Index.cshtml.cs`'s `MileageDisplay`/`VehicleDisplay`
(`origin/dev:.../Assessment/Index.cshtml.cs:253-288`) cascaded saved
assessment value → confirmed → extracted fact → lookup observation into the
Assessment ribbon's Mileage and Vehicle items. The Case ribbon
(`Details.cshtml:111-142`) carries seven different items and neither figure,
and `_CaseVehicle.cshtml` renders confirmed values only for Make, Model and
Mileage (verified above). So an extracted-but-unconfirmed make/model/mileage
value that an operator could previously see on the Assessment page now
displays nowhere on the Case page.

Checked whether D30 or D49 already settle this, and conclude neither does:
D30 moves the Engineer workbench sections (Damage/Valuation/Estimate/
Settlement/Report) onto the Case page and says nothing about the Vehicle
section's field precedence; D49 fixes the intake *population order*
(extraction first, then an automatic DVLA/DVSA lookup) and assigns the
vehicle-record *extension* beyond registration/make/model/mileage to
CASE-043, and suggestion chips for make/model/mileage to CASE-029 — neither
statement authorizes or forbids also surfacing the extracted-fact tier in
the Vehicle section's primary display. Rejecting the "D30/D49 already
settles it" disposition as not honestly supportable; recording the gap
instead.

`_CaseVehicle.cshtml` and the Case ribbon are outside ENG-034's owned/
must-touch paths (`files.md`), so CLAUDE.md's ticket-scope rule ("touch only
the paths its plan and files documents name") forbids fixing this in this
PR. Linking it to CASE-029 and CASE-043 instead: D49 names those two tickets
as the owners of vehicle-field population and its chip/suggestion display,
so whoever picks up either should decide whether the Vehicle section's
primary fields (or a chip alongside them) should also surface an
extracted-but-unconfirmed fact, or whether losing that visibility is an
accepted consequence of retiring the Assessment page. Recording this here so
D49's lane does not read `CaseVehicleSectionShowsLookupEvidence` /
`ConfirmedVehicleFactsTakePrecedenceOverLookupObservation` as proof the
extracted-fact tier is covered on the Case page — it is not.

### F3 (units clarification) — not reproduced as a defect

`git commit` on the renamed test file emits Git's standard "LF will be
replaced by CRLF" autocrlf notice; the working tree and the committed blob
both carry CRLF line endings consistent with every other file in this
directory (`git diff --stat` shows only the 4 renamed identifier lines
changed). No blob/working-copy mismatch was found to fix.

### Verification run this round (worktree `.worktrees/eng-034`)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,240 passed. |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentVehiclePrefillWebTests"` | 0 | 2 passed (`CaseVehicleSectionShowsLookupEvidence`, `ConfirmedVehicleFactsTakePrecedenceOverLookupObservation`). |

No test was weakened or deleted; only names changed. Not merged; PR #668
remains open for the epic owner/review controller to re-review and merge.
