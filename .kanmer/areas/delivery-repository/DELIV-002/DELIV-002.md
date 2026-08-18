---
id: DELIV-002
type: ticket
title: Adopt fast-forward-only dev-to-main releases
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels: []
links: []
deployment: n/a
archived: false
created: '2026-08-18T07:59:54.626Z'
updated: '2026-08-18T07:59:54.626Z'
---

## What

Replace the merge-commit release path with a linear `dev` → `main` release strategy so `main` remains an ancestor of `dev`.

## Why

The current two-parent release merge makes `main` one commit ahead of `dev` even when it adds no unique content. Returning that synthetic merge commit to `dev` is content-redundant and obscures the intended branch relationship.

## Approach

- Update the repository delivery guidance and release mechanism to use fast-forward-only promotion from `dev` to `main`.
- Replace the main-history guard that requires two-parent merge commits with checks that enforce the chosen linear release invariant.
- Preserve shared branch history: do not rebase, reset, or force-push `dev` or `main`.

## Verification

- [ ] A release promotion succeeds only when `main` is an ancestor of `dev` and leaves both refs at the same commit.
- [ ] The main-history CI guard accepts the fast-forward release and rejects a direct, non-release update.
- [ ] After a release, `git merge-base --is-ancestor main dev` succeeds and no `main` → `dev` synchronization merge is required.

## Outcome
