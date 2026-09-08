---
id: INTK-064
type: ticket
title: Automatically associate Triage with its uniquely matched formal Case
status: verifying
area: intake-processing
assignee: intake_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T23:51:19.646Z'
  review: '2026-09-08T06:07:49.364Z'
  verifying: '2026-09-08T06:17:25.019Z'
taken_at: '2026-09-08T05:16:20.212Z'
branch: INTK-064-triage-link-recovery
worktree: .worktrees/intk-064
claim_expires_at: '2026-09-08T06:55:48.858Z'
claim_controller: intake_audit
lease_id: 002a6af1-7ec8-435c-920c-66323b0ace95
lease_revision: 11
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-064'
lease_provider: codex
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T06:25:48.858Z'
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
commits:
  - 1e4f20e5718ec97abf269563fbba1fd944b944a4
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/699'
deployment: not-deployed
delivery_state: not-integrated
delivery_recorded_at: '2026-09-08T06:06:01.134Z'
archived: false
created: '2026-09-07T23:50:36.541Z'
updated: '2026-09-08T06:25:48.858Z'
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
one-ticket supplemental run; original218 roster unchanged. Root approved
execution after INTK-063 closeout and TICK-085's narrow integrated-source
handoff. Taken at .worktrees/intk-064 on INTK-064-triage-link-recovery from
aefe4c32d078ad79c0368666b5666032e6865248. Root's focused verification passed;
PR #699 at 1e4f20e5718ec97abf269563fbba1fd944b944a4 now awaits independent
exact-head review. No queue/schema/framework or Worker grant was added.
Historical claims and the separate original roster remain unchanged.

## Outcome

Implemented creation/replay, formal acceptance/replay and existing timer
association. Root verified Release build, 32 Core and 14 integration tests
(including genuine arrival orders and restricted Worker), plus 127 document
link checks. The report retains the earlier compile failure. Not integrated
or deployed; independent review and exact-merge proof remain outstanding.
