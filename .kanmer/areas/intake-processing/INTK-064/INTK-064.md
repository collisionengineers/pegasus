---
id: INTK-064
type: ticket
title: Automatically associate Triage with its uniquely matched formal Case
status: backlog
area: intake-processing
assignee: ''
profile: fix
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
updated: '2026-09-07T23:50:36.541Z'
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
