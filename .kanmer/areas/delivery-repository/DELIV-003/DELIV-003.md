---
id: DELIV-003
type: ticket
title: Converge shared branches for the first fast-forward release
status: verifying
area: delivery-repository
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-18T08:24:16.980Z'
  review: '2026-08-18T09:23:34.421Z'
  verifying: '2026-08-18T09:26:10.373Z'
taken_at: '2026-08-18T09:22:14.165Z'
branch: task/deliv-003-converge-main-history
worktree: ../pegasus-worktrees/deliv-003-converge-main-history
labels: []
links:
  - DELIV-002
commits:
  - a592beae
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/399'
deployment: n/a
archived: false
created: '2026-08-18T08:17:54.534Z'
updated: '2026-08-18T09:26:10.373Z'
---

## What

After DELIV-002's policy PR is merged into `dev` with CI green, use its
one-time convergence allowance to merge the existing `main` release commit
into this ticket's `origin/dev`-based task branch, deliver that branch through
the normal reviewed PR to `dev`, then make the first exact-SHA fast-forward
promotion from `dev` to `main`.

## Why

Today’s synthetic release merge leaves `main` outside `dev`’s ancestry. One
reviewed convergence is needed before the linear release policy can make both
refs equal without routine return merges.

## Approach

- Start after DELIV-002's PR has landed in `dev` and its CI is green; do not
  wait for its final Kanmer Done stage, which this first promotion enables.
- Use only the one-time, branch-local `origin/main` merge explicitly added by
  DELIV-002; do not directly update `dev`.
- Do not configure GitHub branch protection or rulesets; that option is
  intentionally out of scope on subscription grounds.
- Require explicit `MERGE AUTH GRANTED` for the exact remote release refs
  before any `main` update. Never rebase, reset, or force-push either shared
  branch.

## Verification

- [ ] The reviewed convergence preserves every existing commit and makes
  `origin/main` an ancestor of `origin/dev`.
- [ ] The reviewed `origin/dev` SHA is promoted without force and both remote
  branch heads then equal that SHA.
- [ ] The revised `main` guard passes on the resulting push.

## Outcome
