# Plan — DELIV-003: converge shared branches for the first fast-forward release

## Diff estimate

One task-branch merge plus a normal PR to `dev`; no application code or
GitHub configuration. The only possible tracked-file changes are
`docs/current-architecture.md` and `docs/operations.md`, but only if
post-release observation shows either current-state snapshot changed. The
remote `main` update is a separate, explicit-authority action after the PR.

## Approach

First land DELIV-002's reviewed policy on `dev`; then create DELIV-003 from
that exact remote branch and use its single-use allowance to merge the existing
`origin/main` history locally. The reviewed PR carries that merge into
`dev`. Once it is merged and CI is green, an operator holding explicit
`MERGE AUTH GRANTED` for the then-current remote refs promotes the exact
reviewed `dev` SHA to `main` without force and reads both refs back. This
preserves every shared commit and reaches equality without a routine return
merge. GitHub protections/rulesets remain deliberately out of scope.

## Governing docs

No PRD, FRD, or ADR applies: `docs/index.md` routes repository delivery
guidance to `docs/engineering.md` and task safety to `AGENTS.md`. This plan
uses the versions merged by DELIV-002. `docs/current-architecture.md` and
`docs/operations.md` are current-state owners, not policy owners: reread them
after the source promotion and update them only for observed as-built or
deployed-state changes. The current operations rule says a source revision with
no `src/` change is not itself an application-release claim; a documented
no-change determination in proof is expected if that remains true.

## Steps

1. Wait for PR #396 (DELIV-002) to be independently reviewed, merged into
   `dev`, and green. Fetch `origin/dev`; verify it contains the DELIV-002
   commit and inspect its `AGENTS.md` and `docs/engineering.md` for the
   exact-SHA process and single-use branch-local convergence allowance. Stop
   and return to DELIV-002 if either is absent.
2. Create a fresh DELIV-003 task worktree and `task/` branch from that
   `origin/dev` SHA. Fetch `origin/main`, record both starting SHAs, and
   merge `origin/main` into the task branch without rebase, reset, force, or
   any direct update to `dev`. Resolve only genuine merge conflicts; inspect
   parents, log, and diff to confirm all histories survive.
3. Re-read `docs/current-architecture.md` and `docs/operations.md` against
   the actual source-only convergence. If their current-state facts change,
   update them in the task branch before the PR; otherwise record the
   no-change determination in the post-implementation report and later proof.
4. Run the applicable documentation and history/ancestry checks, inspect the
   task diff through the four simplicity lenses, commit, push, and open the
   normal reviewed PR to `dev`. Do not create a GitHub release PR or change
   GitHub settings.
5. After independent review, green CI, and merge of that PR, fetch both remote
   refs anew. Prove `origin/main` is an ancestor of `origin/dev`, record the
   exact reviewed `origin/dev` SHA, and stop unless the user grants explicit
   `MERGE AUTH GRANTED` for those current refs.
6. With that authority, issue exactly one non-force push of the recorded SHA to
   `refs/heads/main`. Fetch both refs immediately; require both to resolve to
   the recorded SHA. Any changed ref, rejected push, unequal head, or failed
   main-push CI stops the release; never repair it with rebase, reset, force,
   or a direct shared-branch write.
7. On merged `main`, record Kanmer proof with starting/converged/released
   SHAs, ancestry and equal-head output, main-push CI URL/result, current-state
   documentation update or no-change determination, and the user authority
   used. DELIV-002 and DELIV-003 can then be verified from that same evidence.

## Verification

- Before the convergence merge: `git fetch origin`, `git rev-parse
  origin/main origin/dev`, and `git merge-base --is-ancestor origin/main
  origin/dev` (expected to fail before convergence).
- Before the main update: `git merge-base --is-ancestor origin/main
  origin/dev` (expected to succeed) and record `git rev-parse origin/dev`.
- After the update: fetch and require `git rev-parse origin/main` and
  `git rev-parse origin/dev` to equal the recorded SHA; inspect the revised
  main-push CI run as passed.
- `pwsh ./scripts/Test-DocumentationLinks.ps1` and the relevant
  Markdown-placement checks for any tracked documentation change.
- The branch's own diff receives the four-lens simplification pass. Proof is
  written only after the exact remote promotion on merged `main`.

## Risks / open questions

- DELIV-002 has not yet been reviewed or merged. Mitigation: do not take or
  create the DELIV-003 branch until its merged `dev` revision has green CI.
- `dev` or `main` may advance between reads. Mitigation: fresh preflight
  immediately before the authority-gated push, exact-SHA push, and immediate
  equal-head read-back.
- No GitHub prevention is configured by accepted subscription-boundary
  decision. Mitigation: explicit authority, structural CI guard, and recorded
  read-back; these remain detective rather than server-side prevention.

## Simplification pass — 2026-08-18

- **Reuse:** used Git's existing merge operation and the DELIV-002 release
  procedure; no new script, guard, branch model, or documentation mechanism.
- **Simplification:** the convergence commit has no tree diff, so no content
  change was invented to make the PR look substantive.
- **Efficiency:** one local ancestry check and no new application work.
- **Altitude:** history convergence remains at the repository-delivery layer;
  no application architecture or runtime policy changed.
