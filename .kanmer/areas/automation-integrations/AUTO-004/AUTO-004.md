---
id: AUTO-004
type: ticket
title: Allow the Automation Actor to retrieve material from Unidentified intake
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
  - intake
links:
  - AUTO-003
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-20T09:27:47.730Z'
updated: '2026-08-20T10:13:57.210Z'
---

## Why

The Automation Actor cannot currently reach Unidentified intake through `/mcp`. The source contains an orphaned Unidentified tool class, and even that unregistered class exposes only aggregate metadata/history rather than the retained receipt or original group members staff can inspect through existing Core use cases.

## Scope

- Register the existing typed Unidentified list/get/resolve tools and correct the governed tool inventory.
- Enrich exact U-reference detail from the same receipt/group sources used by staff.
- Add bounded retrieval of an exact retained receipt source or exact submission-group member through the existing Core download and integrity boundary.
- Keep resolution on `IResolveUnidentified`, use the existing `automation.intake` scope/actor/audit conventions, and add real HTTP caller evidence for success, denial, validation, attribution, grouped material, and integrity failure.
- Reconcile capability/as-built/runtime claims only after caller evidence exists.

Triage is a distinct workflow and is deliberately excluded to [[AUTO-005]]. Broader classified-mail parity remains [[AUTO-003]].

## Verification

- The registered `/mcp` inventory exposes the approved Unidentified tools and fails if they are omitted.
- The actor can inspect and retrieve authorised receipt-origin and submission-group-origin material without a Case.
- Web and Automation resolution use the same Core command and produce equivalent permanent history.
- Case-document identity remains case-scoped; U-references are not accepted as Case/Audit/Image Intake/principal identifiers.
- Scope denial, invalid reference/member/version, integrity failure, and action-history attribution are proven through the real HTTP caller.

## Outcome
