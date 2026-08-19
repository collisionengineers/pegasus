---
id: TICK-215
type: ticket
title: Decide where report rendering executes in production
status: implementing
area: documents-reports
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:57:22.311Z'
taken_at: '2026-08-19T09:25:01.208Z'
branch: task/tick-215-renderer-execution-decision
worktree: ../pegasus-worktrees/tick-215-renderer-execution-decision
labels:
  - now
  - source-now
  - decision-required
groups:
  - EPIC-004
links:
  - SIMPLI-015
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/adr/0028-run-integrated-renderer-in-web-container-app.md
commits:
  - 169bcd5bbe1e334a52dbb18725d1ae46c6e8f6ab
  - 4d1bff3db4ed16692e7646ea07e7f4491365defd
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/413'
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.967Z'
updated: '2026-08-19T09:25:11.682Z'
---

## What

Decide where report rendering executes in production.

## Why

The historical renderer-relocation question required a durable production execution boundary that kept CollisionRenderer integrated into Pegasus rather than creating a separate system.

## Approach

ADR-0028 resolves the choice: render in process inside the existing Pegasus Web Container App. The Web image carries the pinned Chromium/native/font runtime, the existing Flex Consumption Worker remains unchanged, and Pegasus adds no renderer-specific app, job, service, API, MCP host, package, repository, queue consumer, or deployment unit.

## Verification

- [x] The task plan defines the owned decision, failure/evidence boundary, and acceptance evidence.
- [x] Completion is recorded only at the architecture-decision evidence tier; integration, container readiness, deployed capacity, and operator acceptance remain unclaimed.

## Notes

- Source: the retired pre-Kanmer tracker — Waiting — renderer relocation.
- [[SIMPLI-014]] owns source integration behind the Core render contract.
- [[PLAT-007]] owns Web image, IaC, health, telemetry, capacity, recovery, and separately approved deployment proof.
- Future detached execution remains explicitly deferred unless measured evidence shows Web cannot carry the workload and a new ADR is accepted.

## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

The production execution-location decision was delivered by [[DOCS-002]] through accepted ADR-0028 and [PR #413](https://github.com/collisionengineers/pegasus/pull/413), merged to `dev` at `4d1bff3db4ed16692e7646ea07e7f4491365defd` on 2026-08-19. TICK-215 reconciles that completed decision only. It makes no repository, runtime, infrastructure, deployment, Azure, Worker, or `main` change.
