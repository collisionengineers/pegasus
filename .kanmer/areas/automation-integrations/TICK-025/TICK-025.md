---
id: TICK-025
type: ticket
title: >-
  MCP-03 — Automation Actor intake-queue actions through the same Core use cases
  as the QDOS-alpha staff app
status: preparing
area: automation-integrations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T03:48:14.272Z'
labels:
  - capability
  - MCP-03
  - now
  - requires-live-approval
groups:
  - EPIC-005
  - HZN-003
links: []
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
archived: false
created: '2026-08-12T15:03:53.286Z'
updated: '2026-08-20T03:48:14.272Z'
---

## What

Plan and research **MCP-03**: Automation Actor intake-queue actions through the same Core use cases as the QDOS-alpha staff app

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MCP-03.
- Canonical owner: [MCP automation and actor boundary](docs/frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)
- Activation/boundary: Implemented (queue list, durable intake submission on the automation channel) behind the shared composition gate; non-blocking for `0.1.0-alpha.1` acceptance and gated off outside local evidence runs.
