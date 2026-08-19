# Proof — TICK-011

## Verified target

- Merged branch: `origin/main`
- Verified commit: `d8de29cb` (Merge pull request #405)
- Reachable INT-17 delivery commits: `ef3eb4c7` and `ba65c1ed`. Both are
  ancestors of `d8de29cb` (`git merge-base --is-ancestor` succeeds for each).

### Citation correction — 2026-08-19 ([[DELIV-012]])

This section previously read: *"Historical INT-17 commits `ae6f0c2d`,
`ef3eb4c7`, and `f7d99b18` are ancestors of this commit."* Two of those three
SHAs are **not** ancestors of `origin/main`. The objects still exist in the
local object store, but `git branch -a --contains` returns **no refs** for
either — they are unreachable pre-rebase objects, so the citation cannot be
reproduced by a reviewer working from the repository.

The conclusion is unchanged and independently re-verified: the INT-17
capability really is in the deployed tree. `git ls-tree -r --name-only
origin/main` lists **20** ImageIntake paths, including
`src/Pegasus.Core/ImageIntake/*`, `EfImageIntakeStore.cs`, migration
`20260803071539_ImageIntakeRegistration`, the Web pages and the test files.
Only the two unreachable SHAs are withdrawn; `ef3eb4c7` and `ba65c1ed` are the
reachable delivery commits.

## Automated evidence

Command run from a temporary detached `origin/main` worktree on 2026-08-18:

```powershell
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~ImageIntake" --verbosity minimal
```

Result:

```text
Passed! - Failed: 0, Passed: 78, Skipped: 0, Total: 78
```

The temporary verification worktree was removed after the successful run.

## Independent review

An independent reviewer inspected the plan, FRD-06, ADR-0019, current caller and policy boundaries, and the cited history. Verdict: pass. The reviewer confirmed that the plan covers the implied scope, the implementation evidence covers the plan, and the no-diff simplification disposition is honest.

## Qualification

The earlier wider integration subset timed out without a final result and is not claimed as passing. This proof establishes the focused merged-main regression evidence required by the ticket.

**Deployment versus activation (corrected 2026-08-19).** The `deployment` field
previously read `not-deployed`, which is false about the shipped code: the
ImageIntake source, migration, Web pages and tests are demonstrably present in
the deployed release-10 tree. The field now reads `production` and the accurate
qualification is recorded here — **the code is shipped; there is no production
caller execution.** That is an activation fact, not a deployment one, and it is
tracked in this ticket's `open-questions`. Release scoping must not count
TICK-011 as undeployed work awaiting a future release.
