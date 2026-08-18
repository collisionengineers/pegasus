---
id: DELIV-004
type: ticket
title: Prohibit shipping features behind disabled gates
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-18T09:14:39.400Z'
  review: '2026-08-18T09:23:04.809Z'
  verifying: '2026-08-18T09:25:10.376Z'
  done: '2026-08-18T12:22:08.400Z'
taken_at: '2026-08-18T09:21:33.793Z'
branch: task/deliv-004-no-gated-features
worktree: ../pegasus-worktrees/deliv-004-no-gated-features
labels:
  - policy
  - source-now
links:
  - AUTO-001
commits:
  - ea908247b222376c2dc7f25cf825bfdca98a822a
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/398'
archived: false
created: '2026-08-18T08:50:16.059Z'
updated: '2026-08-18T12:22:08.400Z'
---

## Why

A feature that is compiled or merged but disabled by a composition or feature gate is not shipped. The repository guidance must make this a hard rule so that implementation, review, release, and current-state claims cannot treat gated-off behaviour as delivered.

## Verification

- `AGENTS.md` unambiguously prohibits shipping or claiming a feature as implemented while its required gate is off.
- The rule requires an explicit activation plan and evidence before a feature may be described as shipped.
- Repository workflow and documentation claims remain consistent with the rule.

## Outcome
