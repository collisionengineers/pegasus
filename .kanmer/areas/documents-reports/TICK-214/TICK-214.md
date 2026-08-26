---
id: TICK-214
type: ticket
title: Decide the long-term MCPB host and distribution boundary
status: done
area: documents-reports
order: 2100
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:03:59.534Z'
  implementing: '2026-08-25T06:50:06.674Z'
  review: '2026-08-25T06:50:06.999Z'
  verifying: '2026-08-25T06:50:23.330Z'
  done: '2026-08-25T06:50:23.635Z'
labels:
  - now
  - source-now
groups:
  - EPIC-004
links:
  - SIMPLI-015
  - SIMPLI-014
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.885Z'
updated: '2026-08-26T14:34:46.727Z'
---

## What

Decide the long-term renderer MCPB host and distribution boundary.

## Decision

No renderer MCPB boundary survives. The standalone stdio host, manifest, bundle build/distribution, browser bootstrap, local-output descriptors, MCP-only tests, API/CLI, and separate workspace/runtime are retired. Pegasus rendering is an internal Core-owned application use case implemented by one Infrastructure adapter.

Pegasus's existing authenticated Automation MCP gains no renderer template/payload/path operation. Any future report-status tool requires its own caller-backed Core contract and returns Pegasus identities, never local renderer artifacts.

## Outcome

Closed as a no-code acceptance slice on 2026-08-25. [[SIMPLI-014]] removed and proved the obsolete boundary in PR #415 at `b548b674e31d05de6f43eeb285a25dedd7d2a768`; current `origin/dev` retains no standalone renderer host or distribution surface. TICK-214 created no replacement mechanism, repository diff, PR, deployment, bundle, or cloud action.
