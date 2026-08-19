---
id: TICK-204
type: ticket
title: Define the missing assessment-report outcome variants
status: done
area: documents-reports
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:57:22.284Z'
  review: '2026-08-19T09:10:35.239Z'
  verifying: '2026-08-19T09:17:06.632Z'
  done: '2026-08-19T09:19:07.320Z'
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
  - 545a287d50bc9ab223db632e4c1905e575f1121e
  - 8124ae2abf0ccbe24f57b52703c4dc48e6e6719c
  - 314a9b266560446d25afe4648148181fb27779b8
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/412'
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.231Z'
updated: '2026-08-19T09:22:21.763Z'
---

## What

Define the missing assessment-report outcome variants.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Next — renderer capability questions.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

Shipped as the docs-only [PR #412](https://github.com/collisionengineers/pegasus/pull/412), merged into `dev` on 2026-08-19 as `314a9b266560446d25afe4648148181fb27779b8`. FRD-11 now defines the four canonical assessment-report outcomes and makes the Core-computed VAT-inclusive repair total the Contract repair cap; the owning PR also resolved the [[PR-003]] correction through `8124ae2abf0ccbe24f57b52703c4dc48e6e6719c`.

Deployment: n/a — documentation-only; no cloud write and no `dev` to `main` promotion. Linked follow-up: [[SIMPLI-015]].
