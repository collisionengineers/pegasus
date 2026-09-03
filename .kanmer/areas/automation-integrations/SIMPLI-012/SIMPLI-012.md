---
id: SIMPLI-012
type: ticket
title: Decide the post-alpha disposition of AI and MCP
status: done
area: automation-integrations
order: 410
assignee: ''
profile: feature
stageEntered:
  backlog: '2026-08-17T12:54:14.368Z'
  preparing: '2026-08-20T03:52:53.436Z'
  implementing: '2026-08-20T03:52:53.646Z'
  review: '2026-08-20T03:53:06.568Z'
  verifying: '2026-08-20T03:53:06.714Z'
  done: '2026-08-20T03:53:08.649Z'
labels: []
groups:
  - EPIC-002
  - EPIC-005
links: []
blocks: []
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
deployment: n/a
archived: false
created: '2026-08-13T12:12:48.943Z'
updated: '2026-09-03T09:06:44.269Z'
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

- [x] The decision and its resulting scope are recorded and actionable.

## Decision (2026-08-20)

**RESUME / ACTIVATE — not remove.** Operator direction: *"consider the 'qdos alpha' restriction relaxed in terms of scope… All MCP related tickets are within your scope now"* (operator, 2026-08-20).

The "dormant" premise in this ticket's original framing (2026-08-17) is stale: MCP has been live in production since release 10 (2026-08-18) — `docs/operations.md:230`, `infra/modules/platform.bicep:425` (`Features__AutomationMcp=true`), confirmed by a live read-only probe on 2026-08-20 (`/mcp` → 302, `/connect/token` → 400, `/authorize` → 400 — none 404).

**Resulting scope:** all MCP tickets are active. Concretely: MCP-05 (TICK-062) is already `implementing`, taken by another lane concurrently with this decision being recorded; MCP-03 (TICK-025) was found fully implemented against its own committed capability scope and closed `done` in this same run; MCP-07 (TICK-104) remains in scope per the operator's direction but still governed by its own `Later/1.3.0` allocation and its `TICK-102` dependency, unaffected by this decision beyond being confirmed in-scope. See `research.md` for full evidence.
