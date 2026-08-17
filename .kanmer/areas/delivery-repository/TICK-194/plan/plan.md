# Plan — TICK-194: Detect direct or non-merge pushes to main in CI

## Approach

Add a repository-owned PowerShell validator and invoke it from the existing full-history `changes` job only for `push` events targeting `main`. The validator will inspect the explicit event `before..head` first-parent segment and require every newly introduced mainline commit to be a merge commit. Keeping the policy in a script makes the workflow small and enables deterministic architecture tests with synthetic Git repositories. This is detection after a push, not branch-protection prevention.

## Governing docs

The ticket has no linked PRD, FRD, or ADR because this is repository workflow policy rather than product behaviour or a durable application architecture choice. The existing authoritative rules are `AGENTS.md` and `docs/engineering.md`: work reaches `main` through a reviewed `dev` merge commit and protected branches are not rewritten. The implementation enforces those existing rules without modifying their meaning. `docs_todo` remains set because Kanmer only accepts PRD/FRD/ADR links for the governing-doc field, while repository process is explicitly governed elsewhere.

## Steps

1. Create the required task worktree from `origin/dev`, claim the ticket with the exact branch/worktree, and add the root task plan at `docs/temp-plans/main-branch-history-guard.md`.
2. Add `scripts/Test-MainBranchHistory.ps1` to validate explicit before/head revisions, ancestry, a non-empty first-parent segment, and two-parent merge shape for every new mainline commit, with actionable failure output.
3. Add a guarded step to `.github/workflows/ci.yml` immediately after full-history checkout in the always-running `changes` job, using `github.event.before` and `github.sha` only for pushes to `main`.
4. Add architecture tests that build temporary Git histories and cover an allowed merge-only append, a direct commit, a mixed batch containing a direct first-parent commit, an unavailable revision, the all-zero sentinel, and a non-ancestor rewrite.
5. Run the focused architecture tests, repository documentation/link checks as applicable, Release build, and inspect the final diff for excluded UI/design paths.
6. Write the post-implementation report, commit and push the branch, open a PR targeting `dev`, record traceability, and move the ticket to Review.

## Verification

Run `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --filter FullyQualifiedName~MainBranchHistoryGuardTests`, then `dotnet build --configuration Release` and the repository's applicable documentation/link validation. The synthetic tests must prove both allowed and fail-closed histories. Confirm `git diff --name-only origin/dev...HEAD` contains no `src/Pegasus.Web/**`, UI browser/snapshot, `design/**`, or `.stitch/**` path.

## Risks / open questions

- Git may emit platform-specific wording; tests will assert the script's stable diagnostics and exit status rather than Git's incidental text.
- The zero-before value used by GitHub for branch creation cannot prove an append-only merge and will fail closed.
- The check detects a violation only after GitHub accepts the push; branch protection is explicitly out of scope.
- No open product or implementation questions remain.
