---
id: SIMPLI-012
type: ticket
title: Decide the post-alpha disposition of AI and MCP
status: backlog
area: automation-integrations
order: 180
assignee: ''
profile: feature
stageEntered:
  backlog: '2026-08-17T12:54:14.368Z'
labels: []
groups:
  - EPIC-002
  - EPIC-005
links: []
blocks: []
archived: false
created: '2026-08-13T12:12:48.943Z'
updated: '2026-08-17T12:54:14.368Z'
---

## What

Make an explicit post-alpha decision to resume AI/MCP work with an activation plan or remove it.

## Why

Dormant AI and MCP surfaces should not continue to consume active simplification scope without a delivery decision.

## Approach

- Assess the work after alpha acceptance.
- Record either a concrete activation plan or a removal decision.

## Scheduling note (2026-08-17)

By its own wording this ticket runs **after alpha acceptance** ([[HZN-003]] is not yet reached), so it was released from Implementing back to Backlog rather than left taken. Its branch `task/simpli-012-ai-mcp-disposition` and worktree were removed with no commits. Inputs that will feed it when it activates: [[SIMPLI-014]] (renderer MCP/tool consolidation, [[TICK-203]], [[TICK-214]]), the existing `ModelContextProtocol.AspNetCore` host in `Pegasus.Web`, ADR-0011 (Automation Actor), ADR-0021.

## Verification

- [ ] The decision and its resulting scope are recorded and actionable.
