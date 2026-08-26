---
id: DELIV-004
type: ticket
title: Prohibit shipping features behind disabled gates
status: done
area: delivery-repository
order: 400
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-18T09:14:39.400Z'
  review: '2026-08-18T09:23:04.809Z'
  verifying: '2026-08-18T09:25:10.376Z'
  done: '2026-08-18T12:22:08.400Z'
labels:
  - policy
  - source-now
links:
  - AUTO-001
commits:
  - ea908247b222376c2dc7f25cf825bfdca98a822a
  - ac641ceb
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/398'
deployment: n/a
archived: false
created: '2026-08-18T08:50:16.059Z'
updated: '2026-08-26T14:34:43.243Z'
---

## Why

A feature that is compiled or merged but disabled by a composition or feature gate is not shipped. The repository guidance must make this a hard rule so that implementation, review, release, and current-state claims cannot treat gated-off behaviour as delivered.

## Verification

- `AGENTS.md` unambiguously prohibits shipping or claiming a feature as implemented while its required gate is off.
- The rule requires an explicit activation plan and evidence before a feature may be described as shipped.
- Repository workflow and documentation claims remain consistent with the rule.

## Outcome

Rule added to `AGENTS.md` (PR #398, merged 2026-08-18T09:24:59Z as `ac641ceb`); on `main` since release 9. Applied the same day: [[AUTO-001]] activated the Automation MCP gate in production instead of shipping it dark. The stored block on [[DELIV-005]] resolved when that ticket reached Done. Closed out 2026-08-18.
