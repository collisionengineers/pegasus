---
id: PLAT-007
type: ticket
title: >-
  Deploy integrated report rendering through the existing Azure application
  topology
status: done
area: platform-operations
order: 1520
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T03:50:43.739Z'
  implementing: '2026-08-20T03:50:47.610Z'
  review: '2026-08-20T03:51:10.778Z'
  verifying: '2026-08-20T03:51:11.665Z'
  done: '2026-08-20T03:51:14.553Z'
labels:
  - now
  - renderer-integration
  - requires-live-approval
groups:
  - EPIC-004
links:
  - SIMPLI-014
  - TICK-081
blocks:
  - TICK-081
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/adr/0028-run-integrated-renderer-in-web-container-app.md
deployment: production
archived: false
created: '2026-08-19T08:56:26.248Z'
updated: '2026-09-01T14:44:33.100Z'
---

## What

Compose and deploy the monolith-integrated CollisionRenderer capability through Pegasus's existing Azure topology, including Chromium/native dependencies, durable work execution, artifact custody, configuration, health, observability, retry/recovery, and deployment validation.

## Why

The report renderer must be part of Pegasus rather than a separately deployed API, MCP host, package, or service. Local build parity alone does not prove Azure operation.

## Approach

- Reuse the existing Web/Worker, queue, storage, identity, infrastructure-as-code, telemetry, and deployment conventions where they fit.
- Add Chromium/font runtime dependencies to the existing deployable boundary selected by research; do not create a separate deployment unit without a new accepted ADR.
- Keep Azure writes gated on explicit approval for exact targets; local and read-only validation may proceed first.
- Refresh current-state and operations docs after deployment.

## Verification

- [ ] Existing Azure IaC and release workflow build the integrated renderer with matched Chromium/native dependencies.
- [ ] A deployed assessment render completes, persists its artifact/reference, and emits useful telemetry.
- [ ] Retry, timeout, restart, duplicate delivery, and unavailable-renderer behavior are proven fail-closed.
- [ ] No standalone CollisionRenderer Azure service/API/MCP deployment remains.

## Outcome
