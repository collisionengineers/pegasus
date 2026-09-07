---
id: MAIL-036
type: ticket
title: Preserve the wipe-time email cutoff across Graph replay
status: verifying
area: mail-communications
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-07T20:18:52.479Z'
  review: '2026-09-07T20:44:41.973Z'
  verifying: '2026-09-07T20:50:44.730Z'
taken_at: '2026-09-07T20:22:58.731Z'
branch: MAIL-036-wipe-boundary
worktree: .worktrees/mail-036
claim_expires_at: '2026-09-07T21:31:06.463Z'
claim_controller: codex-v1-remediation-root
lease_id: 7b75a627-f815-47ca-af8e-3349bac00c98
lease_revision: 3
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: root-mail-20260907
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\mail-036'
lease_phase: verifying
lease_heartbeat_at: '2026-09-07T21:01:06.463Z'
labels: []
groups:
  - EPIC-014
links:
  - MAIL-031
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - a0260a4ef6fca856d86b4c44c5b0c863d93276b8
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/678'
archived: false
created: '2026-09-07T20:01:20.357Z'
updated: '2026-09-07T21:01:06.463Z'
---

## What

Preserve an explicit processing cutoff when Pegasus intake/mail test data is wiped. Old emails must stay excluded when delta tokens reset, queued notifications arrive late or retained identities have been removed. Preserve mailbox onboarding/configuration and reuse its start boundary where appropriate; one effective oldest admissible receive time applies to both polling and direct notification intake.

## Acceptance

Existing wipe workflow records a current cutoff; focused old/new-message tests after wipe and delta reset show old mail excluded and newly received/forwarded mail accepted. No live wipe is implied by implementing this change. The operator's current request supersedes old replay-from-onboarding behavior.

## Outcome
