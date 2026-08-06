# ADR-0017: Multi-agent task workflow with claim-by-push on dev

- Date: 2026-08-03
- Status: accepted

## Context

Repository work is increasingly executed by several coding agents at once.
The previous tracking rules were single-threaded: `NOW.md` capped `Doing` at
one item, no canonical document mentioned git worktrees or parallel agents,
and three incompatible worktree conventions had appeared on disk with no rule.
`docs/engineering.md` also described feature branches merging directly to
`main`, while the actual delivery flow is feature branches into `dev`, then
`dev` into `main`, with `main` as the active production deployment.

Parallel agents need a way to take a task visibly and race-free. Recording a
claim by editing a file on a feature branch cannot work: the edit reaches
other agents only after the eventual merge. The one store every clone and
worktree shares promptly is the remote ref state — a push to `origin/dev`
either lands or is rejected, atomically.

## Decision

1. `NOW.md` remains the only work tracker. Its claimable unit is a task line:
   goal text first, capability IDs referenced when they apply, and one line
   may bundle several small features. Non-feature work (redesigns, refactors,
   documentation) is a task line with plain goal text. One task = one
   worktree = one PR.
2. Branch model: task branches are cut from `dev` and merge into `dev`
   through a PR; `dev` merges into `main` through a PR; `main` is the active
   deployment. `dev` and `main` are never rebased, reset, or force-pushed.
3. A claim is a commit that edits only `NOW.md` — moving the task line into
   `Doing` with branch name, date, and agent — pushed directly to `dev`. A
   rejected push means `dev` moved: fetch, reset the task worktree to the
   new `origin/dev` (discarding the unpushed claim commit), re-read `Doing`,
   and re-commit. Maintenance pushes to `dev` are limited to `NOW.md`
   task-line changes and `docs/temp-plans/` deletions.
4. Task worktrees live at `../pegasus-worktrees/<task-slug>` on branch
   `task/<task-slug>`. The authoritative copy of `NOW.md` is the one on
   `origin/dev` after a fetch.
5. Each claimed task records its implementation plan as
   `docs/temp-plans/<task-slug>.md` on the task branch. These are the only
   Markdown files besides ADRs that may be created, and they are transient:
   the post-merge maintenance push deletes them. Before a task PR merges, an
   agent that did not implement the task answers two questions against the
   plan: did the plan miss anything the task line implied, and did the
   implementation miss anything from the plan.
6. Merge authority is split. A task PR merges into `dev` when the
   `repository-check` jobs for its head revision succeeded or were
   path-skipped and the independent plan review passed; the implementer may
   perform that merge. `MERGE AUTH GRANTED` from the operator is required
   only for merging `dev` into `main`. Enforcement is by rule, not branch
   protection.
7. The build/test CI jobs run only when a build-relevant path changes —
   application source, tests, the solution, project/lock/configuration
   files, or a CI-executed script; the documentation link check still runs
   on every PR.

`docs/engineering.md` owns the full protocol text; this record fixes the
decisions only.

## Consequences

Agents discover in-flight work from `Doing` on `origin/dev` and from
`git worktree list` / `git branch --list 'task/*'`, and cannot take the same
task twice because the claim push serializes on the `dev` ref. `NOW.md`
merge conflicts when several PRs land close together are expected and
resolved by taking `dev`'s copy and reapplying only the PR's own line change.
Claim lines ride into `main`'s `NOW.md` when `dev` is released; this is
accepted as cosmetic. Stale claims are removable by anyone under the
staleness ladder in `NOW.md`. The blanket prohibition on merge/reset git
operations is narrowed to protect only work an agent does not own.

## Addendum: docs-only carve-out (2026-08-06)

Operator decision, 2026-08-06. A task is docs-only when every path in its
final PR diff is a Markdown file outside `src/`, `tests/`, `infra/`, and
`scripts/`. A docs-only task:

1. skips the transient `docs/temp-plans/<task-slug>.md` plan file
   (decision 5's plan requirement does not apply to it), and
2. has its two-question independent review answered against the PR diff
   and description instead of a plan file: did the PR miss anything the
   task line implied, and did the diff change anything the task line did
   not authorise.

Everything else in this decision is unchanged: the claim-by-push, the
worktree and branch convention, one task = one worktree = one PR, the
independent review before merge, green CI, and merge authority. A task
that stops qualifying mid-work — its diff gains a non-Markdown path or
one inside the excluded trees — writes the plan file before review, as
decision 5 requires. `docs/engineering.md` owns the protocol text.
