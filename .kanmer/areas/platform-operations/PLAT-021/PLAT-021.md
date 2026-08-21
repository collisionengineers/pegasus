---
id: PLAT-021
type: ticket
title: >-
  Deduplicate application exceptions and page only for failed or persistent
  operations
status: preparing
area: platform-operations
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-21T09:58:24.302Z'
labels:
  - production
  - monitoring
  - OPS-08
  - alert-quality
links:
  - MAIL-003
  - PLAT-013
  - CASE-005
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/adr/0002-dotnet-modular-monolith-on-azure.md
archived: false
created: '2026-08-21T09:57:55.118Z'
updated: '2026-08-21T09:58:24.302Z'
---

## Why

The current Sev1 rule pages for every AppExceptions row. Successful operations and duplicated telemetry create noise and obscure actionable failures.

## Acceptance

The infrastructure-owned rule deduplicates by operation and normalized signature, alerts for a failed recent request or persistence across distinct operations/minute buckets, preserves the existing action group, and historical replay distinguishes the permission incidents from a recovered deadlock.

## Outcome
