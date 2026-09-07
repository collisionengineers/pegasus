---
id: DOCS-020
type: ticket
title: Keep report snapshots consistent and invalidate changed report inputs
status: done
area: documents-reports
assignee: pack_reconcile
profile: fix
stageEntered:
  preparing: '2026-09-07T20:21:48.709Z'
  review: '2026-09-07T21:40:35.885Z'
  verifying: '2026-09-07T21:51:25.688Z'
  implementing: '2026-09-07T22:43:54.189Z'
  done: '2026-09-07T23:18:03.038Z'
taken_at: '2026-09-07T20:28:04.764Z'
branch: DOCS-020-report-consistency
worktree: .worktrees/docs-020
claim_expires_at: '2026-09-07T23:26:12.321Z'
claim_controller: codex-v1-remediation-root
lease_id: 4e050162-b3c0-4605-bc4b-c3f50fb7b38f
lease_revision: 10
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: pack-reconcile-docs020
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\docs-020'
lease_phase: verifying
lease_heartbeat_at: '2026-09-07T22:56:12.321Z'
labels: []
groups:
  - EPIC-014
links:
  - CASE-047
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - 1bf9ac613a2b7610d2ddc23e8acfd9f4b462ef79
  - 522e67f270ab4d6086d9fba04095988db3598888
  - 2b1d700ba70336560934b171fa623d8c66df5559
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/682'
  - 'https://github.com/collisionengineers/pegasus/pull/685'
archived: false
created: '2026-09-07T19:58:09.568Z'
updated: '2026-09-07T23:18:03.038Z'
---

## What

Repair PR675 report review findings: freeze report inputs from one guarded Case version; invalidate report generations on source-document and signatory changes using existing contracts; refuse stale delivery; use LondonCalendar for civil report dates and operator presentation.

## Acceptance

Focused existing persistence tests cover a concurrent source mutation, adding/removing source documents, changing eligibility/name/qualifications/signature and refusing stale delivery. A BST-midnight test proves report dates. No new framework or domain policy owner. Current operator permission includes implementation, merge and deployment; return an independently reviewable PR.

## Outcome
