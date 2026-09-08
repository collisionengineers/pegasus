---
id: UIIMP-017
type: ticket
title: Use one office-time display and one reproducible Health snapshot state
status: verifying
area: ui-improvement
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T00:42:19.213Z'
  review: '2026-09-08T02:33:30.782Z'
  verifying: '2026-09-08T02:38:06.618Z'
taken_at: '2026-09-08T01:56:09.050Z'
branch: UIIMP-017-health-display
worktree: .worktrees/uiimp-017
claim_expires_at: '2026-09-08T03:08:06.683Z'
claim_controller: codex-mcp-client
lease_id: 8e604775-3cb1-4b8a-b036-2bb719330cc6
lease_revision: 4
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\uiimp-017'
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T02:38:06.683Z'
labels: []
groups:
  - EPIC-014
links:
  - UIIMP-005
  - PLAT-069
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - 3e585f6e0f43ed0d90773be967ead867bc5e76f1
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/693'
archived: false
created: '2026-09-08T00:39:38.232Z'
updated: '2026-09-08T02:38:06.683Z'
---

## What

Correct the confirmed Health metrics UTC/ambient-culture rendering and ambiguous Test UI default selector identified in the supplied PR675 reviews and pegasus_pack/current/uiimp-005-snapshot-diagnosis.md. Reuse OperatorLabels.OfficeTime and the existing named populated-mailbox Health scenario. No broad timestamp normalization, global clock change, new harness or full recapture.

## Acceptance

The five metrics instants use the same Europe/London display as the service rows; the snapshot requires the recorded graph_unavailable mailbox scenario and rejects incompatible successful responses. Focused actual page checks and scoped fresh capture/verify/catalogue pass.

## Scope and preservation

Historical UIIMP-005 implemented the underlying Test UI gate via merged PR609 but retains an old foreign claim and superseded PR588 pointer. Preserve it; this ticket owns only the newly diagnosed remaining Health defect. Root alone verifies. No live/cloud/mail changes.
