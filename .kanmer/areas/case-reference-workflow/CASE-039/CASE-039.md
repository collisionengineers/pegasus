---
id: CASE-039
type: ticket
title: 'Engineer notes: append-only staff notes to the Engineer as a Case section'
status: review
area: case-reference-workflow
assignee: wf-build/case-039
profile: feature
stageEntered:
  preparing: '2026-09-02T22:08:14.568Z'
  review: '2026-09-04T21:58:15.845Z'
taken_at: '2026-09-04T20:53:49.421Z'
branch: task/case-039-engineer-notes
worktree: .worktrees/case-039
claim_expires_at: '2026-09-04T21:23:49.421Z'
claim_controller: wf-build/case-039
lease_id: 8ec6d665-d007-48d5-8b05-5443d98b9642
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-039'
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T20:53:49.421Z'
labels:
  - case
  - notes
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/669'
archived: false
created: '2026-09-02T20:31:38.723Z'
updated: '2026-09-04T21:58:15.845Z'
---

## What

A Case section where staff leave notes for the Engineer, attributed and append-only, separate from the Notes history.

## Why

D32. Mockup source: `Pegasus_UI_v2_src/src/21-case-sections.js` §engineer-notes.

## Approach

- Reuse the Triage append-only note shape (INTK-054); one table, one migration with grants.

## Verification

- [ ] Notes are attributed and cannot be edited or deleted.
- [ ] They do not appear in the Notes history.

## Outcome
