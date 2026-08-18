---
id: DELIV-003
type: ticket
title: Converge shared branches for the first fast-forward release
status: preparing
area: delivery-repository
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-18T08:24:16.980Z'
labels: []
links: []
deployment: n/a
archived: false
created: '2026-08-18T08:17:54.534Z'
updated: '2026-08-18T08:24:16.980Z'
---

## What

After [[DELIV-002]] is merged into `dev`, perform the one-time non-rewriting convergence of the existing `main` release commit into `dev`, then make the first exact-SHA fast-forward promotion from `dev` to `main`.

## Why

Today’s synthetic release merge leaves `main` outside `dev`’s ancestry. One convergence is needed before the linear release policy can make both refs equal without routine return merges.

## Approach

- Wait for DELIV-002’s policy, guard, CI, and test changes to be merged into `dev`.
- Do not configure GitHub branch protection or rulesets; that option is intentionally out of scope on subscription grounds.
- Require an explicit `MERGE AUTH GRANTED` for the exact remote release refs before any `main` update.
- Never rebase, reset, or force-push either shared branch.

## Verification

- [ ] The convergence preserves every existing commit and makes `origin/main` an ancestor of `origin/dev`.
- [ ] The reviewed `origin/dev` SHA is promoted without force and both remote branch heads then equal that SHA.
- [ ] The revised `main` guard passes on the resulting push.

## Outcome
