---
id: DELIV-017
type: ticket
title: Record production release 28
status: done
area: delivery-repository
order: 1020
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-25T01:21:20.111Z'
  review: '2026-08-25T01:22:42.464Z'
  verifying: '2026-08-25T01:25:33.788Z'
  done: '2026-08-25T01:25:55.922Z'
labels: []
links:
  - ENG-016
  - TICK-222
docs_todo: true
commits:
  - 21084ce5
  - 7afd18037acfa78927c4b4ffdf8e0f74c7ecc688
prs:
  - '541'
deployment: production
archived: false
created: '2026-08-25T01:21:14.319Z'
updated: '2026-09-01T14:44:32.622Z'
---

Record the already completed exact-SHA production deployment of ENG-016 in `docs/operations.md` and refresh `docs/current-architecture.md`. Evidence is source `7e9465b0`, image `sha256:08f5f605…`, Web revision `--7e9465b00603`, migrations `20260824123336_DropEvaHandoffTables` and `20260825001401_RemoveWorkflowCompletenessWaivers`, successful production smoke, nine enabled Worker functions, and verified database permission matrices. Documentation only; no second deployment.
