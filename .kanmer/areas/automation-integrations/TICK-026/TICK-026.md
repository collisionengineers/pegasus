---
id: TICK-026
type: ticket
title: >-
  MCP-04 — Automation Actor document actions through the same Core use cases as
  the staff app
status: done
area: automation-integrations
order: 650
assignee: grok-shell-kanmer
profile: feature
stageEntered:
  preparing: '2026-08-17T13:05:50.615Z'
  review: '2026-08-17T13:39:23.549Z'
  verifying: '2026-08-18T11:12:26.582Z'
  done: '2026-08-18T12:22:36.260Z'
labels:
  - capability
  - MCP-04
  - now
  - requires-live-approval
groups:
  - EPIC-005
  - HZN-003
links: []
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
commits:
  - 0a15d656de56e54924e3fbff6f120e4360b7ff4e
  - 7c0387cc
  - e108ec87
  - 6cf9b166
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/393'
deployment: production
archived: false
created: '2026-08-12T15:03:53.304Z'
updated: '2026-08-26T14:34:43.699Z'
---

## What

Plan and research **MCP-04**: Automation Actor document actions through the same Core use cases as the staff app

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [x] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [x] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MCP-04.
- Canonical owner: [MCP automation and actor boundary](docs/frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)
- Activation/boundary: Implemented (lease-guarded add, download, export) behind the shared composition gate; enabled in production since release 9.

## Outcome

FRD-10 evidence gap closed: HTTP `/mcp` caller tests for add/download/export (success, replay, validation, scope denial, action history) on a shared test harness (PR #393, merged 2026-08-18 as `6cf9b166`; reviewer added the per-tool download/export coverage in `e108ec87`). Verified 15/15 on `main` `f1e116c6`; the tools are live in production behind the enabled gate ([[AUTO-001]]). Not proved: an external Claude client exercising the document tools ([[TICK-023]] tier 5). Closed out 2026-08-18.
