---
id: INTK-064
type: ticket
title: Automatically associate Triage with its uniquely matched formal Case
status: implementing
area: intake-processing
assignee: intake_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T23:51:19.646Z'
taken_at: '2026-09-08T05:16:20.212Z'
branch: INTK-064-triage-link-recovery
worktree: .worktrees/intk-064
claim_expires_at: '2026-09-08T06:10:01.893Z'
claim_controller: intake_audit
lease_id: 002a6af1-7ec8-435c-920c-66323b0ace95
lease_revision: 4
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-064'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T05:40:01.893Z'
labels:
  - triage
  - association
  - durability
  - v1-remediation
groups:
  - EPIC-014
links:
  - INTK-060
  - INTK-033
  - INTK-035
  - INTK-059
  - TICK-035
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-03-triage.md
deployment: not-deployed
archived: false
created: '2026-09-07T23:50:36.541Z'
updated: '2026-09-08T05:40:01.893Z'
---

## What

Wire automatic Triage-to-formal-Case association in both arrival orders and
recover it through the existing reconciliation timer. Reuse the current Core
case matcher and existing Triage transaction/history, with a system-worker-only
entry that does not relax manual staff authorization or case edit leases.

## Why

The current operator brief requires automatic linking where unambiguous.
Read-only audit at dev baafa29e0f7002b8235aa43bf333f5d9bb172828 found only
manual Web/MCP ILinkTriageCase callers; Triage creation and formal acceptance
never attempt this association. Existing persisted principal/origin and Case
match identity provide the needed evidence without another matching engine.

## Acceptance

- Triage-first and formal-Case-first arrival produce one attributed link when
  accepted principal/current identity identifies one non-contradictory Case.
- Replay/reconciliation retries failed work without duplicate histories, lost
  updates, fake staff actors, or overwriting deliberate manual unlink/relink.
- Unknown principal, ambiguous/conflicting identity, cancellation and a live
  staff Case edit lease remain fail-closed; currentness is checked at mutation.
- Triage keeps its permanent reference, findings and workflow; association
  neither converts findings into definitive Case facts nor allocates another PO.

## Coordination

Supplemental EPIC-014 fix authorized by root; linked historical tickets retain
all claims and evidence. [[INTK-059]] already has principal storage in current
code despite its board status. [[TICK-035]] owns current matcher activation;
implementation must follow its merged/released ownership. Root owns a separate
one-ticket supplemental run; original218 roster unchanged. Preparation only,
no take/branch/source edits until root plan review. No queue/schema/framework
or broad Worker grant absent concrete evidence and root approval.

## Outcome
