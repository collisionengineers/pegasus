---
id: CASE-041
type: ticket
title: >-
  Inspect-at fast update from claimant, repairer, storage location and principal
  address history
status: review
area: case-reference-workflow
assignee: wf-build/case-041
profile: feature
stageEntered:
  preparing: '2026-09-02T22:09:37.240Z'
  review: '2026-09-04T19:44:07.304Z'
taken_at: '2026-09-04T18:27:38.085Z'
branch: task/case-041-inspect-at-choices
worktree: .worktrees/case-041
claim_expires_at: '2026-09-04T18:57:38.085Z'
claim_controller: wf-build/case-041
lease_id: 16f288ff-81d8-4b10-bda1-da11cb3b8ec0
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-041'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T18:27:38.085Z'
labels:
  - case
  - inspection
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links:
  - INTK-058
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
prs:
  - '664'
archived: false
created: '2026-09-02T20:31:38.819Z'
updated: '2026-09-04T19:44:07.304Z'
---

## What

Inspect at becomes a choice: Image Based Assessment, Claimant address, Repairer location, Storage location, previous addresses used for this principal, Manual entry; options without a recorded value are disabled; the Case records a storage location.

## Why

D33. Mockup source: `Pegasus_UI_v2_src/src/21-case-sections.js` §inspection, `05-state.js` (`inspectAtOptions`).

## Approach

- Extend the CASE-027 inspection-address partial and the Core resolution; principal history is a query over the principal's cases, no new table; storage location is one column with grants.

## Verification

- [ ] Choosing an option with a recorded value fills the address; Manual keeps the input.
- [ ] Repairer location is offered disabled with its condition until a repairer address exists on the case (no repairer address is persisted anywhere today; [[INTK-058]] extracts one from the instruction material). Amended 2026-09-03 by operator answer.
- [ ] History lists distinct previous addresses newest first.

## Outcome
