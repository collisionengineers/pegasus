# Plan — DELIV-002: Adopt fast-forward-only `dev` → `main` releases

## Diff estimate

Five tracked files: two delivery-policy documents, the existing PowerShell
guard, its CI invocation, and its architecture tests. No new project,
abstraction, repository Markdown file, GitHub setting, or remote ref update is
part of this ticket.

## Approach

Use the existing main-push guard, augmented with the checked-out
`origin/dev` ref, to prove the structural condition
`before ≤ main head ≤ dev`. This permits an exact fast-forward of an existing
`dev` commit and rejects both a direct `main` commit outside `dev` and the
synthetic release merge GitHub would create. It cannot identify the person who
pushed a structurally valid commit, so explicit `MERGE AUTH GRANTED` and the
pre-/post-push procedure remain the authorization control. The complete,
canonical release procedure belongs in `docs/engineering.md`; `AGENTS.md`
states the authorization and allowed-operation boundary without duplicating the
command sequence. GitHub branch protection and rulesets remain deliberately out
of scope on subscription grounds.

The historic convergence and first remote promotion are a separate, blocked
unit in [[DELIV-003]]. It needs an explicit authorization for the exact shared
refs and must not be folded into this task's single worktree/PR.

## Governing docs

This ticket has no PRD, FRD, or ADR refs, and none is needed. The verified
routing is `docs/index.md`: repository delivery guidance belongs in
`docs/engineering.md`, while task claims, authority, and allowed Git
operations belong in `AGENTS.md`. The change does not alter product behaviour
or introduce a durable application architecture decision.

## Steps

1. Replace the merge-commit release wording in `docs/engineering.md` with
   the canonical manual release procedure: fetch both remote refs, prove
   `origin/main` is an ancestor of `origin/dev`, record the reviewed
   `origin/dev` SHA, non-force push that exact SHA to `main`, fetch again,
   and require both remote heads to equal it. State that GitHub PR merge,
   rebase, squash, reset, and force push do not satisfy this procedure.
2. Align the repository task workflow in `AGENTS.md` with that procedure:
   `MERGE AUTH GRANTED` remains mandatory for a `dev` → `main`
   promotion, and the allowed-operation list permits only the documented
   non-force exact-SHA promotion. Keep the no-rewrite rule and refer readers
   to `docs/engineering.md` for the single detailed command sequence.
3. Revise `scripts/Test-MainBranchHistory.ps1`, reusing its existing
   `Before`/`Head` and `RepositoryPath` handling. Add a release-branch
   argument, retain all-zero, unavailable-revision, and append-only checks,
   remove the two-parent requirement, and fail when `Head` is not an
   ancestor of the resolved release branch.
4. Keep the existing `main`-push job in `.github/workflows/ci.yml`, but
   fetch `refs/heads/dev` into `origin/dev`, pass it to the revised guard,
   and rename the step to describe the ancestry check. Preserve the full
   checkout and every change-classification output.
5. Replace the merge-only architecture-test expectations with temporary local
   `main` and `dev` histories: an exact fast-forward passes; a later
   `dev` commit still permits the earlier released `main` head; a direct
   `main` commit and a GitHub-style merge commit fail because neither is in
   `dev`. Retain the malformed-`Before` and rewritten-history coverage.
   Do not test or claim unobservable human authorization.
6. Run the focused architecture tests plus the canonical restore and Release
   build; run the applicable documentation checks. Inspect the branch's own
   diff through the reuse, simplification, efficiency, and altitude lenses,
   recording the required dated disposition in this plan. Open the single
   task PR to `dev` only after those checks and independent review.
7. Hand the remote transition to [[DELIV-003]] after this PR is merged into
   `dev`. That ticket alone performs the one-time convergence and first
   promotion once the user grants `MERGE AUTH GRANTED` for the then-current
   remote refs.

## Verification

- `dotnet restore`
- `dotnet build Pegasus.slnx --configuration Release --no-restore`
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`
- `pwsh ./scripts/Test-DocumentationLinks.ps1`
- The local history fixtures prove accepted fast-forward ancestry and rejected
  non-release main heads. [[DELIV-003]] will provide the remote preflight,
  non-force-push, equal-head, and post-push-CI evidence after explicit release
  authority.

## Risks / open questions

- `dev` can advance between the release push and CI's ref fetch. The guard
  intentionally checks that `main` is contained in the current `dev`
  history, not fragile equality; the release operator's immediate post-push
  check establishes equality at the promotion point.
- Without GitHub-side protection or rulesets, CI is detective. A structurally
  valid direct fast-forward cannot prove who authorized it; this is an accepted
  subscription-boundary decision, recorded in research and open questions.
- No open questions remain. The historical convergence is explicitly deferred
  to [[DELIV-003]], rather than rewriting shared history or widening this
  ticket's one-PR scope.
