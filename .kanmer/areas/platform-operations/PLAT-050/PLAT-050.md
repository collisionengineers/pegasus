---
id: PLAT-050
type: ticket
title: 'Principal settings dialog: EVA API toggles and the Provider API credential'
status: implementing
area: platform-operations
order: 750
assignee: codex-v1-remediation-root
profile: feature
stageEntered:
  preparing: '2026-09-08T05:18:10.731Z'
taken_at: '2026-09-08T07:19:28.448Z'
branch: PLAT-050-principal-contact
worktree: .worktrees/plat-050
claim_expires_at: '2026-09-08T07:49:28.448Z'
claim_controller: codex-v1-remediation-root
lease_id: f6595bc8-36a0-4f6f-a2a9-4d3b2f5750a1
lease_revision: 1
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: root-plat050
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\plat-050'
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T07:19:28.448Z'
labels:
  - ui
  - wave-4
  - principals
  - credentials
groups:
  - EPIC-011
  - EPIC-009
  - EPIC-014
links:
  - PLAT-028
  - TICK-058
  - TICK-061
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-08-28T08:35:24.106Z'
updated: '2026-09-08T07:19:28.448Z'
---

## What

Wave 4 of [[EPIC-011]]. Second pass on `Pages/Administration/Principals/**` after [[PLAT-028]]: the Settings dialog with read-only route addresses (FRD-09), the two ADR-0034 EVA API submission toggles (fold `EvaSubmission.cshtml` in), and the Provider API credential controls (generate/show once, reset, revoke, pause, resume with reason) backed by [[TICK-061]] and delivered together with the [[TICK-058]] submission endpoint (D8).

## Owns

`src/Pegasus.Web/Pages/Administration/Principals/**`, tests.

## Blocked by

[[PLAT-028]], [[TICK-061]], [[TICK-058]].

## Current v1 continuation — 8 September 2026

The original scope and dependencies above remain the historical requirement.
The current operator requests one customer identity and a test Principal
`pegasustest` with contact e-mail `digital@collisionengineers.co.uk`.
Root authorizes preparation only of the minimum contact continuation: an
optional contact e-mail in existing Principal creation, retained on the same
customer row and displayed in Settings. No contact-edit feature, e-mail
sending, routing/domain change, auto-fill, new service, or live data write.

Already-integrated settings/EVA/credential acceptance is mapped in research;
it is not silently replaced by this continuation. ADR-0038 explicitly
supersedes the old automatic EVA toggle. [[TICK-058]]/[[TICK-060]] retain their
separate provider-result contract reconciliation; this ticket does not mark
them accepted. [[EPIC-014]] supplies current authorization and verification
rules, while existing [[EPIC-011]]/[[EPIC-009]] membership/history is retained.

Preparation is not take/implementation authorization. Root must read the
whole plan and settle the shared runtime-role test and generated-index
handoffs before execution.
