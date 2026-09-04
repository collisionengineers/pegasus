# Post-implementation report — PLAT-069

Branch: `task/plat-069-operations-notice`; worktree `.worktrees/plat-069`;
base `origin/dev` at `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`; head
`74124b7fbec5f7bfc267c292b77925b9de41a9fa`.
PR: https://github.com/collisionengineers/pegasus/pull/657

## Files changed (4 commits, all owned paths)

- `src/Pegasus.Core/Operations/ServiceHealth.cs` — added
  `ServiceHealthPolicy.HasPartialData(ServiceHealthSnapshot)`: true only for
  a `Partial` or `Failed` row; ignores `Running`, `Configured`,
  `ReviewRequired`, and `ExternalWorkLimitReached`.
- `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs` — covers all
  states above plus the ignored limit flag.
- `src/Pegasus.Web/Pages/Operations/Index.cshtml` — Service health table
  (formerly lines ~149-187) deleted entirely. New administrator-only,
  anchorless, label-only health notice rendered when
  `serviceHealth is not null && ServiceHealthPolicy.HasPartialData(...) &&
  User.IsInRole(StaffRoleNames.Administrator)`. Existing limit notice kept,
  explanatory sentence removed. `Index.cshtml.cs` unchanged, as planned.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — new
  `OperatorLabels.OperationsNotices` block (delimited by a `// PLAT-069`
  comment) with `ServiceHealth` and `PartialData` constants. The four
  existing `ServiceHealth*Name` helpers are retained, unchanged, for
  PLAT-051's Administration table.
- `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` — table-absence
  assertion, Administrator sees the D37 notice,
  `[Theory(X-Test-Roles: Engineer|User)]` proves neither sees it, no
  `href=""` assertion, `RecordingOperationsStore.LimitReached` made
  configurable, and a combined limit+health test.
- `docs/design/test-ui/catalogue.json` — added the `operations--partial-data`
  state under the existing `/Operations` entry.
- `docs/design/test-ui/index.html` — regenerated (adds the new Operations
  state link).
- `docs/design/test-ui/pages/operations--default.html`,
  `operations--empty.html` — regenerated (content diff: 2 lines each, the
  removed table and rewritten limit notice).
- `docs/design/test-ui/pages/operations--partial-data.html` — new snapshot.
- `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` — added the one
  `["operations--partial-data"] = new(">Service health</strong>")`
  `StateMatches` entry.

No Administration page, code-behind, migration, governing document,
`ci.yml`, `scripts/*.ps1` (beyond the required run), shell/CSS/JS, or
unrelated snapshot file was touched.

## Deviation and self-correction

Codex ran the full (unscoped) snapshot capture, since this repo's
`Update-TestUiSnapshots.ps1` does not yet have a `-Scope` flag. The full
capture rewrote every catalogue page's line endings (LF→CRLF stat noise,
confirmed with `git diff` per file — zero content diff). Before committing
I reverted every regenerated page outside Operations with `git checkout --`
so the final diff is scoped to this ticket's owned paths only; verified
`git status --porcelain` afterward.

## Commands and exit codes (self-run verification, in `.worktrees/plat-069`)

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0
  (0 warnings, 0 errors)
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
  --configuration Release --no-build` — exit 0 (1225 passed)
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj
  --configuration Release --no-build` — exit 0 (100 passed)
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  --configuration Release --no-build --filter
  "FullyQualifiedName~OperationsWebTests"` — exit 0 (26 passed)
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` — exit 0 (54 routed
  sources, 59 prototypes, 0 broken local references)
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
  -SkipCapture` — exit 0 (1 passed)
- Re-run after simplification pass: build — exit 0; Core tests — exit 0
  (1225 passed); Architecture tests — exit 0 (100 passed); OperationsWebTests
  — exit 0 (26 passed)
- No migration in this ticket, so `Test-MigrationGrants.ps1` was not run
  (per plan).

Codex's own earlier run (before my re-verification) additionally reported:
`dotnet test ... "Category!=Corpus&Category!=Browser"` filter is what the
checklist named but the §Build policy instructs focused project/class runs
instead — those are what is recorded above; `Update-TestUiSnapshots.ps1`
full capture — exit 0 (browser capture 120 passed, non-browser capture 300
passed, snapshot update 1 passed).

## Snapshot artifact facts (opened and inspected)

| File | Bytes | Begins with `<!DOCTYPE html>` | Contains ` href=""` |
| --- | ---: | --- | --- |
| `operations--default.html` | 33099 | yes | no |
| `operations--empty.html` | 26675 | yes | no |
| `operations--partial-data.html` | 33305 | yes | no |

`operations--partial-data.html` contains "Service health" and does not
contain the removed explanatory sentence or `service-health-title`.

## Simplification pass

Run by gpt-5.6-sol (low) over the branch diff. One finding (inline the two
`OperationsNotices` labels as Razor literals instead of a new
`OperatorLabels` block) was rejected: the "labels only in
`Presentation/OperatorLabels.cs`" rule is binding (CLAUDE.md, EPIC-011
context.md), not a style preference. Full disposition recorded in
`plan/plan.md` under "## Simplification pass (2026-09-04)".

## Deviations from the plan

None material. The plan's checklist named the solution-wide filtered test
command; the ticket's binding §Build policy (EPIC-012 context.md) instead
requires focused project/class runs, which is what was actually executed
and is recorded above (checklist annotated accordingly). All other steps
followed the plan as written after its 2026-09-03 review revisions.
