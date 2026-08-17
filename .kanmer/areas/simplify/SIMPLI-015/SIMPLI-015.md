---
id: SIMPLI-015
type: ticket
title: >-
  Record renderer + document-extractor integration-into-repo direction; re-scope
  SIMPLI-013/SIMPLI-014
status: backlog
area: simplify
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-002
  - EPIC-004
links:
  - SIMPLI-013
  - SIMPLI-014
  - KANMER-001
  - KANMER-002
docs_todo: true
archived: false
created: '2026-08-14T13:18:49.985Z'
updated: '2026-08-17T06:40:00.679Z'
---

## What

Operator decision (2026-08-14): the report renderer and the document extractor are being **integrated into the Pegasus repository**, not extracted as standalone packages/repos. Record this direction durably (ADR) and re-scope the two contrary in-progress tickets.

## Why

[[SIMPLI-013]] (extractor → standalone .NET package) and [[SIMPLI-014]] (renderer → standalone) are in-progress in the opposite direction. Meanwhile the `docs/temp-plans/report-renderer-integration*.md` plan set — which matches the confirmed direction — is due for deletion under [[KANMER-002]], and the renderer decision tickets TICK-203–TICK-216 are archival candidates under [[KANMER-001]]. Without a durable record, the direction decision exists only in session history and the aligned planning content could be lost twice over.

## Approach

- Author the governing ADR: integrating a workspace into the application requires an accepted ADR plus an integration contract and caller-backed proof (repo invariant — a workspace never joins `Pegasus.slnx` without one). One decision per ADR; the behavioural consequences go to the owning FRD.
- Re-scope or archive [[SIMPLI-013]] and [[SIMPLI-014]] to match the integration direction, with migration notes.
- Before [[KANMER-002]] deletes the temp-plans renderer set, carry forward still-needed planning content (seam options, MCP consolidation, docs migration, the 2026-08-03 open-question resolutions) into this ticket's research or the ADR.
- Coordinate with [[KANMER-001]] so the renderer cluster TICK-203–TICK-216 is consolidated/retargeted here rather than blindly archived.

## Verification

- [ ] Accepted ADR records the integration direction and contract for both workspaces.
- [ ] SIMPLI-013 / SIMPLI-014 re-scoped or archived with migration notes.
- [ ] Disposition of the temp-plans renderer content and TICK-203–TICK-216 recorded here; nothing needed was lost.

## Outcome
