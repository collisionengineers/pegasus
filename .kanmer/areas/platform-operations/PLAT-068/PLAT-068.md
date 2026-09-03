---
id: PLAT-068
type: ticket
title: Sign-off Engineer account setting with qualifications and signature image
status: implementing
area: platform-operations
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-09-02T20:53:21.795Z'
taken_at: '2026-09-03T20:59:06.544Z'
branch: task/plat-068-sign-off-account
worktree: .worktrees/plat-068
claim_expires_at: '2026-09-03T21:29:06.544Z'
claim_controller: claude-code
lease_id: e68edca2-8741-4cdb-ace5-412ef02b57fb
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\plat-068'
lease_phase: implementing
lease_heartbeat_at: '2026-09-03T20:59:06.544Z'
labels:
  - administration
  - accounts
  - sign-off
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - CASE-040
  - ENG-029
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-02T20:31:38.788Z'
updated: '2026-09-03T20:59:06.544Z'
---

## What

Staff accounts in the Engineer role gain a Sign-off Engineer setting with qualifications and a stored signature image; the accounts table shows it; only flagged accounts are offered as sign-off.

## Why

D31; three signatures exist (Andy, Neil, Ed) and not every Engineer signs. Andy is the default; Neil's qualifications are recorded later by an Administrator. Mockup source: `Pegasus_UI_v2_src/src/17-admin.js` accounts dialog.

## Approach

- Extend the PLAT-027 account settings dialog; reuse the brand signature assets; one migration with grants.

## Verification

- [ ] Administrator-only, reasoned, recorded in Action Logs.
- [ ] Renderer reads the sign-off tuple (DOCS-017).

## Outcome
