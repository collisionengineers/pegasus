---
id: INTK-063
type: ticket
title: Recover Image-initiated Case pairing using current accepted identity
status: implementing
area: intake-processing
assignee: pack_reconcile
profile: fix
stageEntered:
  preparing: '2026-09-07T23:51:17.244Z'
taken_at: '2026-09-08T02:44:23.546Z'
branch: INTK-063-image-link-recovery
worktree: .worktrees/intk-063
claim_expires_at: '2026-09-08T04:00:44.184Z'
claim_controller: root
lease_id: 96040972-2231-4565-8a48-d176ea4c6aec
lease_revision: 7
lease_worker_run: pack_reconcile
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-063'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T03:30:44.184Z'
labels:
  - image-intake
  - pairing
  - durability
  - v1-remediation
groups:
  - EPIC-014
links:
  - TICK-042
  - INTK-039
  - INTK-060
  - INTK-061
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
deployment: not-deployed
archived: false
created: '2026-09-07T23:50:36.423Z'
updated: '2026-09-08T03:30:44.184Z'
---

## What

Repair the existing image-to-formal-Case pairing path in both arrival orders.
Use current CaseMatchIndex registration, reject known-principal contradictions,
respect deliberate staff unlink, and recover failed link/merge transitions on
the existing reconciliation timer and acceptance replay.

## Why

Read-only audit at dev baafa29e0f7002b8235aa43bf333f5d9bb172828 found silent
per-item/acceptance catches, duplicate acceptance skipping pairing, registered
image replay skipping pairing, and no scheduled retry for registered images.
The candidate query still reads immutable InstructionDrafts instead of current
Case identity. Existing tests call pairing directly and miss lost wake-ups.

## Acceptance

- A current unique eligible match links and merges exactly once for both
  arrival orders; a transient failure recovers without an unrelated acceptance.
- Single/grouped registered Awaiting instruction cases are eligible for bounded
  oldest-first recovery; merged/staff-closed/manual-unlinked cases are not.
- Corrected current VRM and known principal govern matching; ambiguous,
  contradictory and post-report candidates never silently link.
- Preserve deterministic operation identities, custody work and permanent
  source/history; report failures and continue unrelated candidates.

## Coordination

Supplemental EPIC-014 fix explicitly authorized by root; does not reopen or
transfer [[TICK-042]], [[INTK-039]], [[INTK-060]] or [[INTK-061]]. Original
218-ticket roster is unchanged; root owns the separate one-ticket run.
Preparation only until root plan review. No new queue, schema, grants or
framework absent concrete evidence and root approval. Root owns heavy checks.

## Outcome
