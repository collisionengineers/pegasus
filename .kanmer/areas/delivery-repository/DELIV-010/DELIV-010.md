---
id: DELIV-010
type: ticket
title: Stop full-history CI checkouts timing out on the 700 MB repository
status: backlog
area: delivery-repository
assignee: ''
profile: fix
labels:
  - ci
  - source-now
links: []
docs_todo: true
archived: false
created: '2026-08-18T14:46:22.005Z'
updated: '2026-08-18T14:46:22.005Z'
---

## Why

On 2026-08-18 the `changes` and `documentation` jobs (both `actions/checkout`
with `fetch-depth: 0` under a 5-minute job cap) were cancelled mid-fetch on
three of five runs (PR #405 twice, PR #406 once, main run 32147904129 once);
every re-run passed in ~20 s. The repository packs to ~680 MiB, so a cold
full-history fetch on a hosted runner sometimes exceeds the cap. Each failure
blocks a release promotion until someone re-runs.

## What

Make the history the jobs actually need cheap: partial clone
(`filter: blob:none` / `tree:0`) for the `changes` job (path classification
and `Test-MainBranchHistory.ps1` need commits/trees, not blobs) and for the
`documentation` job's Markdown-placement history, or a longer timeout with a
recorded reason; then measure. Consider what makes the pack large
(`git rev-list --objects --all | git cat-file --batch-check` top offenders) and
whether an asset belongs in LFS or out of history — a separate decision.

## Verification

- Ten consecutive `repository-check` runs complete the `changes` job in well
  under a minute; no checkout cancellations.
