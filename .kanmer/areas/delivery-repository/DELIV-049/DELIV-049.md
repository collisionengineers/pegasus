---
id: DELIV-049
type: ticket
title: Remove NOW.md and reconcile the canonical documentation authority
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-09-07T20:06:42.765Z'
  review: '2026-09-07T20:11:54.875Z'
  verifying: '2026-09-07T20:20:21.736Z'
  done: '2026-09-07T20:40:54.842Z'
taken_at: '2026-09-07T20:08:06.522Z'
branch: DELIV-049-canonical-docs
worktree: .worktrees/deliv-049
claim_expires_at: '2026-09-07T20:50:20.433Z'
claim_controller: codex-v1-remediation-root
lease_id: eee0c2c4-f5b5-4ca9-b032-5b5d9278d9ee
lease_revision: 2
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: root-docs-20260907
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-049'
lease_phase: review
lease_heartbeat_at: '2026-09-07T20:20:20.433Z'
labels: []
groups:
  - EPIC-014
links:
  - DELIV-038
  - DELIV-045
refs:
  - docs/index.md
commits:
  - 2e50fde474ce35eb32eff8677eb2327cb6aad272
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/677'
delivery_state: integrated
delivery_branch: dev
delivery_sha: 2e50fde474ce35eb32eff8677eb2327cb6aad272
delivery_recorded_at: '2026-09-07T20:40:55.650Z'
archived: false
created: '2026-09-07T20:01:20.404Z'
updated: '2026-09-07T20:40:55.650Z'
---

## What

Remove the reintroduced root NOW.md and special-case allowances/references in AGENTS.md, documentation index and Markdown placement checks. Route current work to Kanmer, product behavior to owning PRD/FRD and as-built/deployed facts to existing current-state docs. Reconcile superseded three-stream/open-unmerged and Linux-only claims with current authorized remediation, coordinating cross-OS release ticket changes.

## Acceptance

No active NOW.md source or reference remains; canonical documents agree with current task and evidence. Preserve protected operator business meaning and mark historical execution mechanics as superseded under the current user's explicit instruction. Keep pack reference originals locally recoverable. No new repository planning source of truth.

## Outcome

Removed duplicate root work index and retired superseded delivery mechanics in PR677, merged into dev at 2e50fde474ce35eb32eff8677eb2327cb6aad272. Independent review and exact-merge Markdown verification PASS. Historical text remains in Git; no runtime/deployment claim. Cross-platform release remains DELIV-048; broader product documentation reconciliation stays with its owning v1 tickets.
