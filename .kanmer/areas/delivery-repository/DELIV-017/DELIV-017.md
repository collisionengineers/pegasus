---
id: DELIV-017
type: ticket
title: Record production release 28
status: implementing
area: delivery-repository
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-25T01:21:20.111Z'
taken_at: '2026-08-25T01:21:38.900Z'
branch: task/deliv-release-28-docs
worktree: 'C:\Users\PC\Documents\GitHub\pegasus-worktrees\release-7e9465b0'
labels: []
links:
  - ENG-016
  - TICK-222
docs_todo: true
deployment: production
archived: false
created: '2026-08-25T01:21:14.319Z'
updated: '2026-08-25T01:21:38.900Z'
---

Record the already completed exact-SHA production deployment of ENG-016 in `docs/operations.md` and refresh `docs/current-architecture.md`. Evidence is source `7e9465b0`, image `sha256:08f5f605…`, Web revision `--7e9465b00603`, migrations `20260824123336_DropEvaHandoffTables` and `20260825001401_RemoveWorkflowCompletenessWaivers`, successful production smoke, nine enabled Worker functions, and verified database permission matrices. Documentation only; no second deployment.
