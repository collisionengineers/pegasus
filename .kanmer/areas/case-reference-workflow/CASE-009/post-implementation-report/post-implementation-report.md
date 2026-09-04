# Post-implementation report — CASE-009 (2026-09-04)

## PR

https://github.com/collisionengineers/pegasus/pull/665
Branch `task/case-009-case-queries-correspondence`, worktree `.worktrees/case-009`,
based on `origin/dev` and merged forward once (fast-forward, no conflicts) to
pick up UIIMP-016 (#662) before finishing.

## Files changed

| Path | What |
| --- | --- |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseQueryEmail` read row; `CaseDetails.QueryEmails` (init, default `[]`) |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Read-only projection of currently linked Queries-destination mail, newest first |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` | Calls the new partial, gated on `QueryEmails.Count > 0` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseCorrespondence.cshtml` (new) | The Queries table |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Column/link labels in a CASE-009-delimited block |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Rendering, absence, no-manual-control coverage |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | EF projection coverage |

No migration, no DI change, no `docs/design/test-ui/**` commit, no
`Details.cshtml` change — all confirmed by `git status`/`git diff --stat`
against `origin/dev` before commit.

## Commands and exit codes

```
dotnet restore ./Pegasus.slnx --locked-mode                                   → 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              → 0 (0 warnings, 0 errors)
dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build       → 0 (1225 passed)
dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build → 0 (100 passed)
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build \
  --filter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~RetainedMailPersistenceTests" → 0 (100 passed, ran twice: pre- and post-simplification-pass)
Update-TestUiSnapshots.ps1 -Scope case-details \
  -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~TestUiFocusedRenderTests" → 0 (capture)
Update-TestUiSnapshots.ps1 -Scope case-details -Verify -SkipCapture           → 0 (byte-identical)
Test-UiCatalogue.ps1                                                         → 0 (54 routed sources, 59 prototypes, 0 broken local references)
```

## Snapshot artifact facts

No snapshot file is part of this PR's diff (confirmed: `git status --porcelain -- docs/design/test-ui/` is
clean after the scoped `-Verify -SkipCapture` run). The one intermediate
capture run (without `-Verify`) that regenerated
`docs/design/test-ui/pages/case-details--default.html`,
`case-details--conflict.html`, `case-details--unavailable.html` and
`index.html` was reverted with `git checkout -- docs/design/test-ui/`
(confirmed no content diff, only `autocrlf` line-ending noise, via
`git diff --ignore-cr-at-eol`).

## Deviations from the plan

1. **Whitespace defect found and fixed (not anticipated by the plan).** The
   first codex implementation pass's `Update-TestUiSnapshots.ps1 -Verify` run
   reported `case-details--default.html` stale. Root cause: `_CaseFiles.cshtml`'s
   unconditional `<partial Cases/Shared/_CaseCorrespondence>` call, plus that
   partial's own leading blank line, each emitted one unconditional newline
   even when `QueryEmails` was empty (the outer `@if` inside the partial did
   not stop the surrounding literal HTML/whitespace from rendering). Fixed at
   the cause — moved the `QueryEmails.Count > 0` gate to the `<partial>` call
   site in `_CaseFiles.cshtml` and removed the now-redundant internal `@if`
   from `_CaseCorrespondence.cshtml` — rather than committing the snapshot or
   relaxing the check, per the "fix at the cause inside owned files" rule.
   Reverified byte-identical after the fix.
2. **Codex's first implementation run stopped early** after running
   `dotnet build` before `dotnet restore` in the fresh worktree (a
   self-inflicted ordering mistake, not a real defect) and treated that as the
   plan's stop condition without adding any tests. Retried once with the
   correct command order and full test requirements; the retry completed the
   three named tests and all production code.
3. **Solution-wide test gate ran as its per-project/filtered constituents**
   (`Pegasus.Core.Tests`, `Pegasus.ArchitectureTests`, the two changed
   `Pegasus.IntegrationTests` classes) rather than the plan's literal
   `dotnet test ./Pegasus.slnx --filter "Category!=Corpus"`, per the standing
   orchestrator local-checks policy (GitHub CI runs the full suite on the PR).
4. **origin/dev advanced by one commit** (UIIMP-016, #662, docs-only) after
   the worktree was created; fast-forward merged with `git merge --no-edit
   origin/dev` before finishing, no conflicts, no owned-file changes from that
   merge.

No other deviation from the plan's acceptance conditions. CASE-038's shared
lock on `Pages/Cases/Shared/*` / `OperatorLabels.cs` was already clear (PR
#656 merged before this worktree was created); CASE-029 and CASE-040 were
`preparing`/not taken throughout, so the shared `EfCaseQueryStore.cs` /
`CaseDetailsWebTests.cs` files were never contended.

## Simplification pass

Recorded in `plan/plan.md` under "## Simplification pass (2026-09-04)" — 3
findings applied (efficiency: constrain the receipt query to the requested
case before the authoritative association check; reuse: the canonical mailbox
source-channel code instead of a literal; reuse/altitude: the existing
persistence-to-Core classification mapping instead of hand-rebuilding
`MailCategory`), 5 findings rejected with reasons. Rebuilt and reran Core,
Architecture and the two changed integration classes after applying — all
green.
