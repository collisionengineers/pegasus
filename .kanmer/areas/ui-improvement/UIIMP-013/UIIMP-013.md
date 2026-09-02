---
id: UIIMP-013
type: ticket
title: Reduce the Test UI snapshot gate critical path without weakening coverage
status: done
area: ui-improvement
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-02T03:09:00.895Z'
  review: '2026-09-02T14:52:45.734Z'
  implementing: '2026-09-02T16:55:41.695Z'
  verifying: '2026-09-02T17:44:31.300Z'
  done: '2026-09-02T18:17:11.269Z'
review_round: 1
labels:
  - ci
  - performance
groups:
  - EPIC-011
links:
  - UIIMP-005
refs:
  - docs/engineering.md
commits:
  - fa7d82ed95c7dc8a0b90f9d22db74118603def75
  - 35667cb176baf31eceaa3eefa77ddb7ec3111ac8
  - 8116ac7b5545149670eb318708a2a4181bdba786
  - cad00be9d42dbeaee9edf34c2d24de222d7ddb9d
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/644'
deployment: n/a
archived: false
created: '2026-08-30T12:51:09.415Z'
updated: '2026-09-02T18:37:42.270Z'
---

## What

Reduce the Test UI snapshot gate's pull-request critical path without weakening
its complete capture, stale-file, orphan, or offline-render guarantees.

## Current evidence

- Historical run `33310451221` spent 40m23s in capture before cancellation.
- Recent successful build-relevant runs completed capture and verify in
  approximately 24–27 minutes before this change.
- The old script pinned the whole capture selection to two threads although
  only browser tests require that cap; non-browser integration tests use the
  repository's proven four-thread default elsewhere.
- Verify is one fact over the retained capture, not a second run of all capture
  tests.

## Decision

Keep the full gate on every build-affecting pull request. Split the exact
capture selection into browser and non-browser phases, retain the browser cap,
let non-browser capture inherit the project cap, reuse the build and capture
directory, and make incomplete-run output distinct from an explicit stale
snapshot assertion.

Do not reuse broader CI lane captures or introduce UI-only scheduling: both
weaken the curated snapshot input or detection boundary.

## Verification

- [x] The same 415 tests run across a disjoint two-filter partition.
- [x] Fresh verify and catalogue pass; unchanged stale/orphan assertions retain
      the linked UIIMP-005 negative-injection proof.
- [x] Three runs of one PR SHA all pass with a 21:32 median and 22:42 maximum.

## Outcome

Implemented in PR #644. Browser capture remains capped at two threads;
non-browser capture inherits four; the second capture and verifier reuse the
build; phase timings are explicit. The measured timeout formula reduced the
snapshot step/job budgets to 35/40 minutes. Final-SHA repository check
`33641477638` passed. PR #644 merged to `dev` at
`cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`; exact-SHA verification passed
and no follow-up ticket is required.
