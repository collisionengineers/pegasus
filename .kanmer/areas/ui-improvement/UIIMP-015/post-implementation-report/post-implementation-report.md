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

## Review round fixes (2026-09-04)

Two blocking findings from the first review round, fixed on the same branch
(no new worktree/ticket — the ticket's own worktree, `.worktrees/uiimp-015`).

### Finding 1 — case-details--default matcher loose against the Review state

Confirmed against the retained scoped-cohort capture: of the 130 case-detail
responses `CaseDetailsWebTests` + `TestUiFocusedRenderTests` produced, only 3
carried State "Review" together with the edit-lease presence strip
(~53.7 KB raw), while the loose matcher (presence strip + `case-overview-panel`
only) let `Generate`'s lexicographically-smallest tiebreak silently pick a
smaller "Not ready" / "Recover editing" candidate — the exact class of bug
this ticket's own approach exists to catch, now caught in its own committed
artifact.

Fix: added a second required substring to the `case-details--default`
`StateMatch` entry, `status status--navy">Review<` — the exact markup
`src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` renders for the Review
lifecycle state (tone `navy`; distinguished from other navy-toned states by
requiring the literal text `Review`, not just the tone class). `StateMatch`
gained a second optional `AlsoRequired2` parameter rather than a new matcher
concept — the existing record already modelled "one required text plus one
optional additional text"; this is the first state needing two.

```csharp
["case-details--default"] = new(
    "You are editing this case.",
    AlsoRequired: "case-overview-panel",
    AlsoRequired2: "status status--navy\">Review<"),
```

Regenerated via the ticket's own scoped procedure
(`-Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"`,
then `-Verify -SkipCapture -Scope case-details`, then
`Test-UiCatalogue.ps1`) — run twice independently (once by the implementer,
once by the reviewing agent applying these fixes, git-fetching `origin/dev`
first and confirming no new commits to merge) with identical results both
times.

Snapshot artifact evidence (superseding the table in the section above for
this one file):

| Path | Bytes before | Bytes after | `<!DOCTYPE html>` | Markers |
| --- | ---: | ---: | --- | --- |
| `docs/design/test-ui/pages/case-details--default.html` | 34,879 | 51,251 | yes | `You are editing this case.` (1), `case-overview-panel` (1), `status status--navy">Review<` (2) |

The other three committed `docs/design/test-ui/pages/case-details--*.html`
and `docs/design/test-ui/index.html` regenerated byte-identical (`git diff
--stat` after `git add` staged only `TestUiSnapshotTests.cs` and
`case-details--default.html`; the other four files showed as modified only
by `core.autocrlf` line-ending normalization in the working tree, with zero
content diff, and were not staged).

No `class="case-sticky"` or `id="section-"` markers appear in the current
`case-details--default.html` (0 matches for both) — checked directly against
`src/Pegasus.Web/Pages/Cases/Details.cshtml`, which uses `record-ribbon` /
`record` markup, not those hooks; the same is true of the page's prior,
already-committed content before this fix, so this is a pre-existing fact
about this route's markup, not a regression introduced here.

### Finding 2 — ParseScope silently degenerate on an all-separator -Scope

Fix: `ParseScope` now asserts the split result is non-empty before returning
it, naming the raw input on failure — matching the file's existing
`Assert.True(... , "message")` house style (see `ValidateScope`):

```csharp
private static string[]? ParseScope(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var scope = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    Assert.True(scope.Length > 0, $"Test UI scope contains no usable prefixes: '{value}'");
    return scope;
}
```

An absent `-Scope` still returns `null` (full, unscoped run — unchanged); a
non-whitespace value that splits to zero prefixes (e.g. `-Scope ","`) now
fails the test explicitly with `Test UI scope contains no usable prefixes:
','` instead of silently generating and verifying only `index.html`.

### Commands and exit codes (review-round fixes)

| Command | Exit | Notes |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,225 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"` (implementer run + independent re-run) | 0 / 0 | 58 non-browser producer tests passed, snapshot update passed 1, both runs |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` (implementer run + independent re-run) | 0 / 0 | Snapshot comparison + offline Chromium render passed 1, both runs |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` (implementer run + independent re-run) | 0 / 0 | 54 routed sources, 59 prototypes, 0 broken references, both runs |
| `-Scope ","` negative check | 1 (expected) | `Test UI scope contains no usable prefixes: ','` |

Capture lock (`scratchpad/capture.lock`) taken before and released
immediately after the independent re-verification run; `origin/dev` fetched
first and confirmed to add no new commits ahead of this branch's head at fix
time.

### Commit

`b7fa4f70c19baae6f93ca30ca52ac75363185868` on
`task/uiimp-015-scoped-test-ui-capture`, pushed. PR #658 unchanged
(https://github.com/collisionengineers/pegasus/pull/658). Neither finding
was rejected; both were fixed as blocking.
