# Files — ENG-028

## Changed

| File | +/− |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | 368 |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | 810 |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | 2 / 0 |
| `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` | 305 |

1160 insertions, 325 deletions. No migration, no package, no new project, no
`Core/` change — this ticket supplies callers for what [[ENG-026]] already built.

## Ownership

`Pages/Cases/Assessment/**` is this lane's by `waves.md`. `OperatorLabels.cs` is
shared: two lines appended inside this lane's own nested class, nothing
reordered, per `decisions-2026-08-29.md` § Two shared files.

Verified clear of every lane in flight at the time: `Pages/Operations/**`
(PLAT-049), `Pages/Administration/**` (PLAT-026, PLAT-027),
`Pages/Cases/Vehicle|Custody|Tasks|Documents` (CASE-027),
`Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` (CASE-012's, untouched),
`Upload*`/`Uploads/**` (INTK-047), `Core/Intake` (DELIV-036), `Core/AiWork` and
`Web/Mcp` (AUTO-014). **None was touched.**

## Salvage from `6b4d11db`

The branch `task/eng-028-estimate-editor` held ~1,676 lines of prior work that
would not cherry-pick.

**Reused:** the multi-estimate editor structure, the Core-port handler shape, the
typed line and import data handling, the JSON route labels, and the test-fake
patterns.

**Rewritten against current `dev`:** the integration into the newer Assessment
shell — preserving its evidence rail and AI-job dialog — plus PLAT-027-style
static dialog targets, removal of the superseded unrendered legacy acceptance
route, and duplicate/use exposed for accepted non-current estimates.

The original branch and its worktree are untouched and can be removed once this
merges.

## Test changes, itemised

Two tests removed, four added; **total assertions 74 → 90**.

| Removed | Why |
| --- | --- |
| `AnExistingDraftRefusesASecondImport` | Encodes "one draft at a time" — the rule the named-estimate model exists to replace |
| `AcceptanceRecordsTheTypedCalculationBasis` | Reworked as `UseEstimateRecordsTheEngineersAcceptance` |

| Added |
| --- |
| `ImportDialogHasAStaticTargetWhenJavaScriptIsUnavailable` |
| `UseEstimateRecordsTheEngineersAcceptance` |
| `TheEditorSavesANamedEstimateWithTypedLines` |
| `DuplicateEstimatePostsToTheNamedEstimateUseCase` |

**Every retained test kept or gained assertions** — 28→29, 5→5, 5→5, 6→6, 4→4 —
and the three import refusals that still matter under the new model survive with
their `Assert.Empty` intact: edit-mode never entered, rejected parse, and
non-Engineer.
