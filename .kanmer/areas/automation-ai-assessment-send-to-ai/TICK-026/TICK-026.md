---
id: TICK-026
type: ticket
title: >-
  MCP-04 — Automation Actor document actions through the same Core use cases as
  the staff app
status: todo
area: automation-ai-assessment-send-to-ai
priority: medium
assignee: ''
labels:
  - capability
  - MCP-04
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.304Z'
updated: '2026-08-12T15:03:53.304Z'
---

## What

Plan and research **MCP-04**: Automation Actor document actions through the same Core use cases as the staff app

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MCP-04.
- Canonical owner: [MCP automation and actor boundary](requirements.md#mcp-automation-and-actor-boundary)
- Activation/boundary: Implemented (lease-guarded add, download, export) behind the shared composition gate; non-blocking for `0.1.0-alpha.1` acceptance and gated off outside local evidence runs.
