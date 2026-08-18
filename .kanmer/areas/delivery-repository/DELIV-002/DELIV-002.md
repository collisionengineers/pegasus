---
id: DELIV-002
type: ticket
title: Adopt fast-forward-only dev-to-main releases
status: review
area: delivery-repository
order: 0
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-18T08:01:12.101Z'
  review: '2026-08-18T08:46:39.169Z'
taken_at: '2026-08-18T08:39:15.115Z'
branch: task/deliv-002-fast-forward-main-release
worktree: ../pegasus-worktrees/deliv-002-fast-forward-main-release
labels: []
links: []
blocks: []
commits:
  - eab23d3d
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/396'
deployment: n/a
archived: false
created: '2026-08-18T07:59:54.626Z'
updated: '2026-08-18T08:46:39.169Z'
---

## What

Replace the merge-commit release path with a linear `dev` → `main` release
strategy so `main` remains an ancestor of `dev`.

## Why

The current two-parent release merge makes `main` one commit ahead of `dev`
even when it adds no unique content. Returning that synthetic merge commit to
`dev` is content-redundant and obscures the intended branch relationship.

## Approach

- Update the repository delivery guidance and release mechanism to use
  fast-forward-only promotion from `dev` to `main`.
- Replace the main-history guard that requires two-parent merge commits with
  checks that enforce the chosen release-branch ancestry invariant.
- Establish the one-time transition: after this ticket's PR is merged into
  `dev` with CI green, [[DELIV-003]] may merge `origin/main` into its own
  `origin/dev`-based task branch and deliver that merge through the normal
  reviewed PR-to-`dev` path.
- Preserve shared branch history: do not rebase, reset, force-push, or directly
  update `dev` for the convergence. The exception ends once it is merged.
- [[DELIV-003]] then owns the first remote promotion under exact
  `MERGE AUTH GRANTED`.

## Verification

- [ ] The policy permits the one-time reviewed convergence PR and thereafter
  requires fast-forward-only promotion from `dev` to `main`.
- [ ] A release promotion succeeds only when `main` is an ancestor of
  `dev` and leaves both refs at the reviewed commit.
- [ ] The main-history CI guard accepts a `main` head contained in `dev`
  and rejects a head outside `dev`; it does not claim to determine the
  human authorization behind a valid fast-forward.
- [ ] After the first release, `git merge-base --is-ancestor main dev`
  succeeds and no routine `main` → `dev` synchronization merge is required.

## Outcome
