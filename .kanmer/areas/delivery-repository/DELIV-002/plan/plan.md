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
pre-/post-push procedure remain the authorization control.

The policy also carries the one-time migration needed to reach that state:
after this ticket's reviewed PR is merged into `dev` with CI green,
[[DELIV-003]] may merge `origin/main` into its own branch cut from
`origin/dev`, then merge that branch through the normal PR-to-`dev` path.
This is a branch-local, non-rewriting exception—never a direct `dev` push—and
it expires when the convergence PR merges. [[DELIV-003]] then performs the
first exact-SHA promotion. The complete canonical release procedure belongs in
`docs/engineering.md`; `AGENTS.md` owns the authorization and allowed
operations without duplicating the command sequence. GitHub branch protection
and rulesets remain deliberately out of scope on subscription grounds.

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
   and require both remote heads to equal it. Add the single transition rule:
   DELIV-003 may first merge `origin/main` into its own
   `origin/dev`-based branch and PR it to `dev`. State that GitHub PR
   merge, rebase, squash, reset, and force push do not replace promotion.
2. Align the repository task workflow in `AGENTS.md`: `MERGE AUTH GRANTED`
   remains mandatory for a `dev` → `main` promotion; permit the one-time
   branch-local convergence merge and normal PR to `dev`; forbid a direct
   `dev` update, shared-history rewrite, or reuse of that exception. Refer
   readers to `docs/engineering.md` for the single detailed command sequence.
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
7. Once this PR has merged into `dev` with CI green, [[DELIV-003]] begins its
   permitted convergence PR and first promotion. Both tickets obtain merged
   `main` proof from that release; neither waits for the other to be Done.

## Verification

- `dotnet restore`
- `dotnet build Pegasus.slnx --configuration Release --no-restore`
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`
- `pwsh ./scripts/Test-DocumentationLinks.ps1`
- The local history fixtures prove accepted fast-forward ancestry and rejected
  non-release main heads. [[DELIV-003]] provides the remote preflight,
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
- The transition exception is intentionally narrow and ends with the
  DELIV-003 convergence PR. No open questions remain.
