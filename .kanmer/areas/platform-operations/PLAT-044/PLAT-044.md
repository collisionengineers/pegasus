---
id: PLAT-044
type: ticket
title: Stop Assessment opening from repeating Review and content-store work
status: preparing
area: platform-operations
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-25T08:47:03.040Z'
labels:
  - qdos26016
  - performance
  - box
  - database
  - operator-reported
links:
  - PLAT-041
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
deployment: not-deployed
archived: false
created: '2026-08-25T08:46:50.919Z'
updated: '2026-08-25T08:48:17.804Z'
---

## What

Make the Assessment GET trust the lifecycle guarantees already established by entry to Review, remove duplicate broad database projections, and defer Box content retrieval until report generation.

## Why

QDOS26016 takes 5–10 seconds to open. Live investigation found that the GET repeats broad case/assessment queries and sequentially downloads confirmed report photographs to recalculate prerequisites that Review has already established. Reuse the batch content-read mechanism from [[PLAT-041]] only when report bytes are actually requested.

## Approach

- Keep report readiness limited to assessment/report-preparation work.
- Replace duplicate GET projections with one page-specific read model.
- Read photographs in one PLAT-041 batch during generation, not page opening.
- Use the persisted Box case-root identity directly for managed content operations.

## Verification

- [ ] Assessment GET performs no document-content reads and uses a bounded database command count.
- [ ] Review prerequisites no longer appear as report-readiness issues.
- [ ] Report generation batch-loads photographs with integrity and order preserved.

## Outcome
