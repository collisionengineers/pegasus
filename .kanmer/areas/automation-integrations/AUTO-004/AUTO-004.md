---
id: AUTO-004
type: ticket
title: Restore Automation Actor parity for Unidentified and Triage
status: preparing
area: automation-integrations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T10:10:55.935Z'
labels:
  - automation-actor
  - MCP
  - unidentified
  - triage
  - intake
  - parity
links:
  - AUTO-003
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/adr/0011-restrict-mcp-to-automation-actor.md
  - docs/adr/0021-automation-actor-direct-write-assessment-contract.md
  - docs/frd/frd-03-triage.md
archived: false
created: '2026-08-20T09:27:47.730Z'
updated: '2026-08-20T10:25:21.385Z'
---

## Why

The accepted Automation Actor boundary grants exactly ordinary `PerformCasework` access and requires a comprehensive same-Core toolset with logging parity. Unidentified tools exist but are unregistered and cannot retrieve retained material; Triage has no MCP caller at all.

## Scope

Deliver [[AUTO-004]] and [[AUTO-005]] in one task/worktree/PR while keeping their typed domain surfaces distinct:

- Register and complete Unidentified list/detail/source/resolve access for receipt and submission-group origins.
- Add Triage list/detail/source and ordinary lifecycle/evidence/Case-association tools over the existing Core owners.
- Use the existing `automation.intake` scope, resolved Automation identity, action auditor, operation keys, versions, evidence rules, integrity checks, and Case leases.
- Correct the governed tool inventory and add real HTTP caller proof.
- Reconcile FRD/capability/as-built/runtime claims only to proven behavior.

Staff-only “Assign to me” identity is not converted into Automation impersonation or an arbitrary-staff assignment API. Broader classified-mail parity remains [[AUTO-003]].

## Verification

- `/mcp` exposes the governed Unidentified and Triage inventory and fails if a tool is omitted.
- Receipt-origin, grouped Unidentified, and Triage retained material are inspectable/retrievable without a Case.
- Web and Automation call the same Core queries/commands and observe the same state, evidence, replay, version, reason, integrity, and Case-lease guards.
- Wrong scope, invalid inputs, stale versions, integrity failures, and prohibited identity/management actions fail closed.
- Every Automation invocation and material denial has attributable permanent history.

## Outcome
