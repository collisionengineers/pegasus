---
id: TICK-213
type: ticket
title: Decide whether density applies to all rendered document bodies
status: done
area: documents-reports
order: 200
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:03:34.137Z'
  review: '2026-08-19T11:26:31.492Z'
  verifying: '2026-08-19T11:37:23.046Z'
  done: '2026-08-19T11:39:33.195Z'
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
commits:
  - 5dbea9589b381deff31197fa57fccd7166ca00c2
  - 14589b8d7a33745134735aca954ec8a91a2ec212
  - 4ba638884df4497cb239e8b36032c201765e723f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/421'
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.829Z'
updated: '2026-09-01T14:44:31.833Z'
---

## What

Decide whether density applies to all rendered document bodies.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [x] The task plan defines the owned density disposition, blocker boundary, stress test and acceptance evidence.
- [x] Completion is recorded at merged-source and real-Chromium stress evidence tier.

## Notes

- Source: the retired pre-Kanmer tracker — Next — renderer density.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.


## Outcome

Normal/default styling applies to the active rendererref1 assessment and fee-note family; no Core, caller, UI, API or MCP density/fit option and no global auto-fit/multipass renderer exists. Long accepted content flows across additional pages at normal density.

The first stress reproduction correctly exposed a separate complete-tail defect, resolved by [[PR-009]] / PR #419 at `4f67a83e22f0b994d5a5f6dbf08d53eec7808a6a`. After merging that current `dev` state, the real-Chromium renderer suite passes 6/6, including 80 entries per work-list family and eight photos with all terminal content, images, Statement/signature and per-page reference furniture retained. TICK-213 changes only the verification-test intent in [PR #421](https://github.com/collisionengineers/pegasus/pull/421) relative to current `dev`; no production, styling, density, deployment, cloud or `main` change was made. PR #421 merged to `dev` at `4ba638884df4497cb239e8b36032c201765e723f`.
