---
id: TICK-023
type: ticket
title: >-
  MCP-01 — Management/development-controlled MCP ingress for one named
  vendor-neutral Automation Actor through Pegasus Core use ca…
status: done
area: automation-integrations
order: 2130
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T03:48:13.904Z'
  implementing: '2026-08-20T03:48:16.263Z'
  review: '2026-08-20T03:48:55.802Z'
  verifying: '2026-08-20T03:48:59.811Z'
  done: '2026-08-20T03:49:05.009Z'
labels:
  - capability
  - MCP-01
  - now
  - requires-live-approval
groups:
  - EPIC-005
  - HZN-003
links: []
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
deployment: production
archived: false
created: '2026-08-12T15:03:53.230Z'
updated: '2026-08-25T01:27:00.932Z'
---

## What

Plan and research **MCP-01**: Management/development-controlled MCP ingress for one named vendor-neutral Automation Actor through Pegasus Core use cases

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MCP-01.
- Canonical owner: [MCP automation and actor boundary](docs/frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)
- Activation/boundary: Implemented behind a composition gate that is off by default (DevelopmentOffline evidence runs only); ordinary staff have no MCP access, a compatible client may provide initial evidence without owning the actor identity, no external product caller is proven yet, live activation stays separately approved, and no AI proposal transport is activated.
