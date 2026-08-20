---
id: AUTO-005
type: ticket
title: Decide the Automation Actor boundary for Triage material
status: backlog
area: automation-integrations
assignee: ''
profile: spike
labels:
  - automation-actor
  - MCP
  - triage
  - intake
links:
  - AUTO-004
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-03-triage.md
archived: false
created: '2026-08-20T10:12:42.306Z'
updated: '2026-08-20T10:12:42.306Z'
---

## What

Determine whether the Automation Actor may list, inspect, retrieve retained material from, or mutate the distinct Triage workflow, and define the smallest typed Core-backed inventory if permitted.

## Why

AUTO-004 confirms that Triage has no MCP tool surface at all. Triage is a separate pre-case workflow rather than an Unidentified state, so adding it to the Unidentified fix would collapse product boundaries and silently broaden actor authority.

## Verification

- The governing FRDs explicitly permit or reject each proposed Triage read/retrieval/mutation.
- Any permitted caller is mapped to the existing Core query/command and staff caller rather than duplicating policy.
- Denied or deferred operations remain absent from the registered MCP inventory.

## Outcome
