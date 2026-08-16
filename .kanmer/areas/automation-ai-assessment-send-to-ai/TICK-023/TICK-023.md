---
id: TICK-023
type: ticket
title: >-
  MCP-01 — Management/development-controlled MCP ingress for one named
  vendor-neutral Automation Actor through Pegasus Core use ca…
status: backlog
area: automation-ai-assessment-send-to-ai
assignee: ''
profile: feature
labels:
  - capability
  - MCP-01
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.230Z'
updated: '2026-08-12T15:03:53.230Z'
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
- Canonical owner: [MCP automation and actor boundary](requirements.md#mcp-automation-and-actor-boundary)
- Activation/boundary: Implemented behind a composition gate that is off by default (DevelopmentOffline evidence runs only); ordinary staff have no MCP access, a compatible client may provide initial evidence without owning the actor identity, no external product caller is proven yet, live activation stays separately approved, and no AI proposal transport is activated.
