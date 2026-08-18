---
id: DELIV-002
type: ticket
title: Adopt fast-forward-only dev-to-main releases
status: preparing
area: delivery-repository
order: 0
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-18T08:01:12.101Z'
labels: []
links: []
blocks:
  - DELIV-003
deployment: n/a
archived: false
created: '2026-08-18T07:59:54.626Z'
updated: '2026-08-18T08:18:42.467Z'
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
- Preserve shared branch history: do not rebase, reset, or force-push `dev`
  or `main`.
- [[DELIV-003]] owns the one-time convergence and first remote promotion after
  this policy change is merged into `dev`.

## Verification

- [ ] A release promotion succeeds only when `main` is an ancestor of
  `dev` and leaves both refs at the reviewed commit.
- [ ] The main-history CI guard accepts a `main` head contained in `dev`
  and rejects a head outside `dev`; it does not claim to determine the
  human authorization behind a valid fast-forward.
- [ ] After a release, `git merge-base --is-ancestor main dev` succeeds and
  no routine `main` → `dev` synchronization merge is required.

## Outcome
