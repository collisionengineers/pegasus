---
id: AUTO-005
type: ticket
title: Expose Triage casework through the Automation Actor
status: preparing
area: automation-integrations
assignee: ''
profile: spike
stageEntered:
  preparing: '2026-08-20T10:23:17.577Z'
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
  - docs/adr/0011-restrict-mcp-to-automation-actor.md
  - docs/adr/0021-automation-actor-direct-write-assessment-contract.md
archived: false
created: '2026-08-20T10:12:42.306Z'
updated: '2026-08-20T10:24:08.277Z'
---

## What

Expose the Triage workflow through typed Automation Actor tools that call the same Core queries and commands as the staff Web caller, and deliver it in the same task/worktree as [[AUTO-004]].

## Why

ADR-0011 requires MCP tools to call the same Core use cases as Web and forbids a second policy engine. ADR-0021 grants `ActorKind.Automation` exactly `PerformCasework` and requires a comprehensive toolset with logging parity. Triage is ordinary casework, but currently has no MCP surface, so this is an implementation omission rather than an unresolved authority decision.

## Scope

Research the exact parity inventory: list/detail, retained source retrieval, state transitions, findings/corrections, response-evidence links, completion/cancellation/reopen, and Case association through existing leases. Preserve explicit identity constraints: the Automation Actor never impersonates staff, so the staff-only “assign to me” interaction is not converted into an Automation assignee or an arbitrary-staff assignment tool.

## Verification

- Every in-scope tool calls the existing Triage/Core owner used by Web.
- Automation receives the same `PerformCasework` authorization, version, reason, operation-key, evidence, and Case-lease guards.
- Staff-only identity semantics and explicitly prohibited external/management actions remain absent.
- Real `/mcp` success, denial, validation, replay, attribution, and history evidence covers the Triage inventory.

## Outcome
