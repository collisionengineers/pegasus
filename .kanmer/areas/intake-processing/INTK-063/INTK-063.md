---
id: INTK-063
type: ticket
title: Recover Image-initiated Case pairing using current accepted identity
status: done
area: intake-processing
assignee: pack_reconcile
profile: fix
stageEntered:
  preparing: '2026-09-07T23:51:17.244Z'
  review: '2026-09-08T04:17:33.123Z'
  verifying: '2026-09-08T04:37:40.981Z'
  done: '2026-09-08T04:53:34.162Z'
taken_at: '2026-09-08T02:44:23.546Z'
branch: INTK-063-image-link-recovery
worktree: .worktrees/intk-063
claim_expires_at: '2026-09-08T05:38:53.019Z'
claim_controller: root
lease_id: 96040972-2231-4565-8a48-d176ea4c6aec
lease_revision: 17
lease_worker_run: pack_reconcile
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-063'
lease_provider: codex
lease_phase: running-command
lease_heartbeat_at: '2026-09-08T04:38:53.019Z'
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
commits:
  - a022fc4b2db87d4d2eeb14437b41f6d8344d63e6
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/696'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: a022fc4b2db87d4d2eeb14437b41f6d8344d63e6
delivery_recorded_at: '2026-09-08T04:55:49.534Z'
archived: false
created: '2026-09-07T23:50:36.423Z'
updated: '2026-09-08T04:55:49.534Z'
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

Integrated into dev by [PR #696](https://github.com/collisionengineers/pegasus/pull/696)
at `a022fc4b2db87d4d2eeb14437b41f6d8344d63e6` on 2026-09-08T04:37:37Z.
Root read the whole exact-merge PASS proof `97b3f0a8b71eb09d` and moved this
ticket Done at 04:53:34.162Z: locked restore/Release build, 75 Core and 18
integration cases passed with no skips. Not deployed; no live/provider or
initial-registration claim from the restricted Worker recovery fixture.

Existing automatic, registered/acceptance replay, staff link and timer callers
now recover current-identity image pairing while preserving recorded staff
intent, reversals, custody and replay. The actual RequestHash correction and
initial-principal hypothesis disposition remain in the independent review
`4859da1b06b2cefe` and report `67f2c76040cf0b1c`. Both prior author failures
remain in proof; all five TRXs are retained with verified hashes at
`pegasus_pack/current/proofs/intk-063/manifest.json` (manifest SHA256
`FCC14EF2269BD14D1F17890D549C344B88DA685E8F75AF2A4B11A51724AE784F`).

Only this ticket's clean roots/branch are authorized for closeout; claim
release is last. [[INTK-064]] may use the released acceptance/timer/matcher
ownership after cleanup. This does not transfer or close historical
[[TICK-042]], [[INTK-039]], [[INTK-060]] or [[INTK-061]] claims. Final v1
release/live acceptance remains with [[EPIC-014]].
