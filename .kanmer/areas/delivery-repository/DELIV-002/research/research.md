# Research — DELIV-002: linear `dev` → `main` releases

## Question

How can Pegasus stop creating a content-redundant `main` → `dev` synchronization merge after a release, while preserving shared history and the explicit release-authority boundary?

## Findings

- The canonical delivery rule in [docs/engineering.md](../../../../../docs/engineering.md#branches-and-delivery) requires `dev` to merge into `main` through a PR *as a merge commit*. [AGENTS.md](../../../../../AGENTS.md#repository-task-workflow) separately gives the `dev` → `main` merge its explicit `MERGE AUTH GRANTED` authority and prohibits rebasing, resetting, or force-pushing either shared branch.
- The current release commit proves the observed divergence: `2b0df78` (PR #394) has first parent `e2020af` and second parent `a6d801b` (the then-`dev` head). Its tree exactly equals its `dev` parent, yet `main...dev` is now `1` main-only and `6` dev-only commits. This was verified with `git show`, `git diff --quiet 2b0df78^2 2b0df78`, and `git rev-list --left-right --count origin/main...origin/dev` on 2026-08-18.
- [scripts/Test-MainBranchHistory.ps1](../../../../../scripts/Test-MainBranchHistory.ps1) currently permits only append-only updates whose new first-parent commits all have two parents. [.github/workflows/ci.yml](../../../../../.github/workflows/ci.yml) runs it only after pushes to `main`; [MainBranchHistoryGuardTests.cs](../../../../../tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs) locks that merge-only behavior into tests.
- GitHub’s default PR merge uses `--no-ff`. Its **Rebase and merge** operation creates new commit SHAs even if a simple fast-forward is possible, so it cannot leave `main` at the existing `dev` SHA. Squash merging similarly creates a replacement commit. GitHub therefore does not expose a PR merge method that preserves the source branch’s SHA as an exact fast-forward. [GitHub merge methods](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/about-merge-methods-on-github), [GitHub PR merges](https://docs.github.com/en/pull-requests/reference/pull-request-merges).
- The 2026-08-18 GitHub configuration read shows `main` has no branch protection and the repository has no rulesets; merge, squash, and rebase methods are all enabled. The current CI guard can report a bad direct push, but it cannot prevent that ref from reaching `main`.
- One non-rewriting convergence step is unavoidable from today’s already-diverged graph: merge the existing `main` release commit into `dev` once. Thereafter, an exact fast-forward can make `main` and `dev` equal at each release without a return merge.

## Implications

- Do **not** rebase `dev` or `main`; that would rewrite shared history and conflicts with the repository workflow.
- The viable target process is: finish the existing one-time convergence, confirm `main` is an ancestor of `dev`, then promote the reviewed `dev` SHA to `main` with a non-force ref update. Git rejects the promotion if `main` changed incompatibly.
- A rewritten GitHub PR merge is not a substitute. Retaining an ordinary GitHub PR merge is incompatible with the ticket’s exact-SHA/equal-head invariant.
- The history script should change from “every update is a two-parent merge” to validating the chosen linear-release invariant. It cannot, by itself and after the event, distinguish an authorized direct fast-forward from an unauthorized direct push; with remote prevention intentionally out of scope, it remains a detective control.
- No product PRD, FRD, or ADR is needed. [docs/index.md](../../../../../docs/index.md) assigns repository-development workflow to `docs/engineering.md` and repository task safety to `AGENTS.md`.

## Open questions

- See [open-questions](../open-questions.md): the release actor/enforcement design needs an owner decision before planning.

## Confirmed direction — 2026-08-18

- GitHub-side branch protection and rulesets are intentionally out of scope on subscription grounds. The target workflow therefore relies on explicit `MERGE AUTH GRANTED` and post-push CI detection; it does not claim to prevent an unauthorized direct `main` update at the GitHub boundary.

- The approved release mechanism is a manual, non-force promotion by the human holding `MERGE AUTH GRANTED`: fetch `origin/main` and `origin/dev`; confirm `git merge-base --is-ancestor origin/main origin/dev`; record the reviewed `origin/dev` SHA; run `git push origin <reviewed-dev-sha>:refs/heads/main`; fetch again and confirm both remote branch heads equal that SHA. A failed preflight, rejected push, or unequal post-push heads stops the release rather than rebasing or forcing either branch.

## CI and repository-gate check — 2026-08-18

- A read-only GitHub API check found no branch protection on `main` (the
  protection endpoint returns HTTP 404) and no repository rulesets
  (`[]`). No server-side required-check, linear-history, or merge-method rule
  will block the policy change or a later direct non-force promotion.
- `.github/workflows/ci.yml` runs on every pull request and on pushes to
  `main`. The existing `Test-MainBranchHistory.ps1` step has the condition
  `push && refs/heads/main`; it does **not** run on the DELIV-002 PR to
  `dev`. Therefore its current two-parent requirement cannot block the PR
  that replaces it.
- The intended files include
  `scripts/Test-MainBranchHistory.ps1`,
  `.github/workflows/ci.yml`, and
  `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs`.
  `Get-CiChangeFlags.ps1` classifies that change set as both
  `Build: true` and `Infrastructure: true`. The PR will run the always-on
  documentation lane, the unit lane (including architecture tests), SQL
  integration shards, browser tests, and the infrastructure lane. These are
  ordinary correctness checks, not policy rejections.
- Once DELIV-002 and DELIV-003 are merged into `dev`, the first exact-SHA
  push to `main` runs the **revised** guard contained in that pushed SHA. It
  should pass only if the new `main` head is contained in the fetched
  `origin/dev` history. If someone promoted before the revised guard reached
  `dev`, the current guard would report failure after the push because a
  fast-forwarded `dev` commit is not a two-parent release merge.
- The CI guard remains post-push detective control. It cannot prevent a bad
  ref update, nor identify the person who authorized a structurally valid
  update; the agreed exact-SHA preflight, post-push read-back, and explicit
  `MERGE AUTH GRANTED` remain necessary.
