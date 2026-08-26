---
id: SIMPLI-015
type: ticket
title: >-
  Record renderer + document-extractor integration-into-repo direction; re-scope
  SIMPLI-013/SIMPLI-014
status: done
area: documents-reports
order: 560
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-17T12:32:50.975Z'
  review: '2026-08-17T12:35:04.019Z'
  verifying: '2026-08-17T12:49:11.790Z'
  done: '2026-08-17T12:52:27.020Z'
labels: []
groups:
  - EPIC-002
  - EPIC-004
links:
  - SIMPLI-013
  - SIMPLI-014
  - KANMER-001
  - KANMER-002
blocks: []
commits:
  - f0057da4
  - 01f300f9
  - 40ee3103
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/389'
deployment: n/a
archived: false
created: '2026-08-14T13:18:49.985Z'
updated: '2026-08-26T14:34:43.534Z'
---

## What

Operator decision (2026-08-14): the report renderer and the document extractor are being **integrated into the Pegasus repository**, not extracted as standalone packages/repos. Record this direction durably (ADR) and re-scope the two contrary in-progress tickets.

## Why

[[SIMPLI-013]] (extractor → standalone .NET package) and [[SIMPLI-014]] (renderer → standalone) were in progress in the opposite direction. The `docs/temp-plans/report-renderer-integration*.md` plan set — which matched the confirmed direction — was deleted under [[KANMER-002]] after being carried into this ticket's research, and the renderer decision tickets TICK-203–TICK-216 were archival candidates under [[KANMER-001]]. Without a durable record the direction existed only in session history.

## Approach

- Author the governing ADR (one decision; mechanics to the owning FRDs and implementation tickets).
- Re-scope [[SIMPLI-013]] and [[SIMPLI-014]] with migration notes.
- Carry the still-needed planning content into this ticket's research; link TICK-203–208, 211–216 from SIMPLI-014.

## Verification

- [x] Accepted ADR records the integration direction and contract for both workspaces — ADR-0025, merged `40ee3103`.
- [x] SIMPLI-013 / SIMPLI-014 re-scoped with migration notes — retitled/rebodied; released to Backlog as `Later` with `refs` → ADR-0025.
- [x] Disposition of the temp-plans renderer content and TICK-203–216 recorded here — research carries the content; TICK links on SIMPLI-014; 209/210 were proof tickets already consolidated.

## Outcome

Shipped in PR #389 (https://github.com/collisionengineers/pegasus/pull/389), merged to `dev` as `40ee3103` on 2026-08-17; docs-only (deployment n/a). ADR-0025 records: integrate when a caller exists, never extract; activation stays gated by ADR-0009. The 2026-08-17 assessment (renderer: strong case, design-tree coupling; extractor: only via the `.doc`/`.msg` caller, resolving ADR-0001/0003 overlap; no package feed exists) is on `research`. Follow-ups: [[SIMPLI-013]], [[SIMPLI-014]] (both `Later`), and the renderer sub-decisions TICK-203–208, 211–216 remain open until activation.
