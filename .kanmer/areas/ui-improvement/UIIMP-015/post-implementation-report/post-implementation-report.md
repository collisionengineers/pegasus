# Post-implementation report — UIIMP-015

## Files changed

- `AGENTS.md` (Test UI command paragraph; `CLAUDE.md` is a symlink to it, so
  it carries the same content with no separate diff)
- `docs/runbook.md` (Test UI snapshot commands section)
- `scripts/Update-TestUiSnapshots.ps1` (`-Scope`, `-CaptureFilter`,
  `PEGASUS_TEST_UI_SCOPE` save/set/restore, Corpus exclusion moved into the
  phase filter builder)
- `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` (scoped generation,
  deletion, comparison, offline render; `ValidateScope`/`MatchesScope`; a new
  `StateMatches` entry for `case-details--default`)
- `docs/design/test-ui/pages/case-details--default.html` (regenerated
  snapshot artifact; content changed because the new `StateMatches` entry
  corrects which captured response the state selects)

No CI, package, catalogue-schema, route, Core, or `OperatorLabels` change.
Five files carry a real content diff; `docs/design/test-ui/index.html` and
`pages/case-details--conflict.html` / `pages/case-details--unavailable.html`
regenerate byte-identical to their committed content (confirmed with
`git diff --quiet`, exit 0 for each, both before and after the simplification
pass).

## Deviation from the plan

The plan's step 2 did not call for a new `StateMatches` entry. Implementation
found that `case-details--default` had no explicit matcher and was previously
selected only by elimination (`otherMatches`); with a scoped, smaller
candidate pool (only `CaseDetailsWebTests` running instead of the full
cohort), that elimination could select a different actual HTTP response than
an unscoped run would. This is exactly what the ticket's "leaves every other
committed page byte-identical" and correctness intent require, so an explicit
`["case-details--default"] = new("You are editing this case.", AlsoRequired:
"case-overview-panel")` entry was added, deterministically selecting the
catalogue's declared Case Overview state. This corrected the previously
committed page, which held a stale not-ready/NOTACTIVE response. Stayed
inside the one owned file (`TestUiSnapshotTests.cs`); no new state, matcher
vocabulary, project, or package was introduced.

## Commands and exit codes

| Command | Exit | Notes |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` (pre-merge, post-merge, post-simplify) | 0 / 0 / 0 | 0 warnings, 0 errors each run |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` (pre-merge / post-merge / post-simplify) | 0 / 0 / 0 | 1,219 / 1,225 / 1,225 passed (count rose after merging origin/dev's PLAT-069) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` (x3) | 0 / 0 / 0 | 100 passed each run |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestUiSnapshotTests"` (x3) | 0 / 0 / 0 | 1 passed each run (compilation/discovery only: `PEGASUS_TEST_UI_MODE` unset, test returns immediately) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"` (x2: pre-simplify, post-simplify) | 0 / 0 | Browser phase matched 0 (exits 0), non-browser phase 58 producer tests passed, snapshot update passed 1 |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` (x2) | 0 / 0 | Snapshot comparison + offline Chromium render passed 1 |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope deliberately-wrong-prefix` | 1 (expected) | `Test UI scope prefixes matched no catalogue state:` `- deliberately-wrong-prefix` |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` (x2) | 0 / 0 | 54 routed sources, 59 prototypes (58 before PLAT-069's Operations partial-data state merged in), 0 broken references |
| `git diff --check` | 0 | No whitespace errors |

No local unscoped capture, whole integration suite, or whole browser suite
was run, per EPIC-012 build policy. The exact-head unscoped `-Verify` runs in
GitHub CI on PR #658.

## Snapshot artifact evidence

| Path | Bytes | `<!DOCTYPE html>` | Expected marker |
| --- | ---: | --- | --- |
| `docs/design/test-ui/pages/case-details--default.html` | 34,879 | yes | `Case Overview` present; also `You are editing this case.` and `case-overview-panel` |
| `docs/design/test-ui/pages/case-details--unavailable.html` | 24,390 | yes | `Case unavailable` present |
| `docs/design/test-ui/pages/case-details--conflict.html` | 34,691 | yes | `case changed` present |

No `<img src="#">` in the default artifact (checked). Non-`case-details`
`docs/design/test-ui/pages/*.html` files (56 files, listed by
`sha256sum ... | sort`): identical path membership and SHA-256 hashes before
and after the scoped capture, checked twice (once before the simplification
pass, once after).

## Merge and simplification

Merged `origin/dev` (PLAT-069's `74124b7fb`/`8f3d09602`, adding the Operations
partial-data snapshot state) cleanly with `git merge --no-edit`; the only
touched shared file, `TestUiSnapshotTests.cs`, auto-merged with no conflict.
Ran the fast checks again after the merge (all green, see table above).

Simplification pass (gpt-5.6-sol, low) over `git diff origin/dev...HEAD`:
4 findings, 1 rejected (AGENTS.md/runbook.md command duplication — matches
the repository's existing cross-doc convention and the plan's own
instruction to update both), 3 applied (dropped a duplicate
`TestUiFocusedRenderTests` filter clause, removed the unused `catalogueRoot`
parameter from `Generate`, replaced a per-comparison single-element array
allocation in `ValidateScope`/`MatchesScope` with a shared
`MatchesScopePrefix` helper). Recorded in the ticket plan under
"Simplification pass (2026-09-04)". Re-ran build, Core, Architecture, the
changed integration test class, the scoped capture/verify pair, the
byte-identity hash check, and `Test-UiCatalogue.ps1` after applying — all
green (see table above).

## PR

https://github.com/collisionengineers/pegasus/pull/658
Branch `task/uiimp-015-scoped-test-ui-capture`, head SHA
`364ae208d8bf0cb439c9cc5f474ce961a9aad691`.
