---
id: DELIV-005
type: ticket
title: Remove Markdown-placement CI gate
status: verifying
area: delivery-repository
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-18T09:33:17.459Z'
  review: '2026-08-18T09:35:00.198Z'
  verifying: '2026-08-18T09:41:54.876Z'
taken_at: '2026-08-18T09:33:53.473Z'
branch: task/deliv-005-remove-markdown-ci
worktree: ../pegasus-worktrees/deliv-005-remove-markdown-ci
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
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/401'
archived: false
created: '2026-08-18T09:33:13.048Z'
updated: '2026-08-18T09:41:54.876Z'
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
