---
id: DELIV-005
type: ticket
title: Remove Markdown-placement CI gate
status: done
area: delivery-repository
order: 600
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-18T09:33:17.459Z'
  review: '2026-08-18T09:35:00.198Z'
  verifying: '2026-08-18T09:41:54.876Z'
  done: '2026-08-18T12:22:04.395Z'
labels:
  - ci
  - rollback
  - source-now
links:
  - TICK-195
  - DELIV-004
blocks:
  - DELIV-004
commits:
  - 015f2e21dc7dba75b347256143bc425701f66a94
  - a80c26ec
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/401'
deployment: n/a
archived: false
created: '2026-08-18T09:33:13.048Z'
updated: '2026-08-25T01:27:00.006Z'
---

## Why

The Markdown-placement workflow step rejects a non-product asset README and
blocks the current `dev` → `main` promotion. The operator has directed its
removal as unnecessary CI policy.

## Verification

- The `documentation` workflow keeps its Markdown-placement regression tests
  and documentation-link validation, but no longer runs the Markdown-placement
  gate against pull-request changes.
- The current release PR no longer fails solely because of
  `src/Pegasus.Web/wwwroot/images/marks/README.md`.
- No source, documentation, or deployment content changes.

## Outcome

Gate removed (PR #401, merged 2026-08-18T09:41:45Z as `a80c26ec`); PR #400's `documentation` job then passed and release 9 promoted the change to `main`. [[TICK-195]]'s validator is thereby rolled back by decision; its regression tests remain. Closed out 2026-08-18.
