---
id: AUTO-004
type: ticket
title: Allow the Automation Actor to retrieve material from Unidentified intake
status: backlog
area: automation-integrations
assignee: ''
profile: feature
labels:
  - automation-actor
  - MCP
  - unidentified
  - triage
  - intake
links:
  - AUTO-003
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-20T09:27:47.730Z'
updated: '2026-08-20T09:27:47.730Z'
---

## Why

The Automation Actor can currently retrieve documents only from cases. It must be able to review material that deterministic intake rules leave in the Unidentified queue, which is a primary automation-review use case.

## Scope

Determine whether to extend the existing document-retrieval MCP tool or introduce a focused intake-material tool. Preserve the existing actor boundary, custody, and fail-closed rules. Confirm explicitly whether equivalent retrieval is permitted for Triage, and record the resulting boundary.

## Verification

- The actor can retrieve authorised Unidentified-queue material without a case.
- Case-only access remains correctly scoped.
- The implemented contract and tests show whether Triage is included or rejected.
- Audit/custody evidence identifies the retrieved intake material and acting principal.

## Outcome
