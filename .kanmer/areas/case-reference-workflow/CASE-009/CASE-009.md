---
id: CASE-009
type: ticket
title: >-
  Show auto-attached Query emails on Case Details and remove manual query
  creation
status: verifying
area: case-reference-workflow
order: 30
assignee: wf-build/case-009
profile: fix
stageEntered:
  preparing: '2026-08-21T07:51:43.337Z'
  review: '2026-09-04T20:15:44.686Z'
  verifying: '2026-09-04T20:49:54.458Z'
taken_at: '2026-09-04T18:27:23.587Z'
branch: task/case-009-case-queries-correspondence
worktree: .worktrees/case-009
claim_expires_at: '2026-09-04T18:57:23.587Z'
claim_controller: wf-build/case-009
lease_id: 4e541da8-e0ec-4226-889e-151776ea58d3
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-009'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T18:27:23.587Z'
labels:
  - ui
  - case-detail
  - queries
  - operator-reported
  - mail-association
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links:
  - CASE-027
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - '665'
archived: false
created: '2026-08-21T07:51:29.215Z'
updated: '2026-09-04T20:49:54.458Z'
---

## Why

The Case Details page must call this section **Queries**, not **Engineer Queries**. It must not offer a **Raise a Query** action: query correspondence is sourced from emails already linked to the Case and classified as a Query.

## Verify

- The heading is **Queries**.
- The Case Details page renders a read-only list of emails linked to that Case whose classification is Query.
- The panel has a truthful empty state when no qualifying linked email exists.
- No **Raise a Query** button or manual query-creation control is present.
- The implementation does not create, reply to, resolve, or manually associate queries, and does not mutate any mailbox.

## Outcome
