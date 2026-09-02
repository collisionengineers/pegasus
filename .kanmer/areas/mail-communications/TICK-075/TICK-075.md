---
id: TICK-075
type: ticket
title: >-
  MAIL-17 — Idempotent report/fee-note send on the original Outlook thread or
  provider API using principal CC/delivery/standing-not…
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - capability
  - MAIL-17
  - now
  - requires-live-approval
  - work-pack-activated
groups:
  - EPIC-011
links:
  - TICK-055
blocks:
  - MAIL-030
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
docs_todo: true
archived: false
created: '2026-08-12T15:05:39.996Z'
updated: '2026-09-01T14:43:23.624Z'
---

## What

Deliver staff-initiated, idempotent report and fee-note sending on the original Outlook thread or approved provider route using Principal delivery/CC/standing-note policy, followed by Box filing and permanent management-event evidence.

## Why

The operator committed the prototype's staff-driven report/fee delivery behavior for EPIC-011. Autonomous correspondence remains excluded: no background rule or approval state may send by itself.

## Approach

- Reuse [[MAIL-027]] approved-mailbox sending, Sent evidence and external-operation idempotency.
- Reuse the accepted report artifact/version and post-report lifecycle; sending never fabricates readiness or closes a Case.
- Resolve and record the exact Principal policy, recipients, thread/provider route and artifact before dispatch.
- Make every retry return/reconcile the same delivery outcome and never duplicate a successful send.
- [[MAIL-030]] owns Administration policy/activity; this ticket owns the delivery use case.
- Update FRD-08/11 and capabilities allocation before implementation leaves Backlog.
- Keep live Outlook/provider activation separately exact-target approval gated.

## Verification

- [ ] One staff-authorized request produces at most one delivery for an artifact/version and destination.
- [ ] Sent/provider evidence, Box filing and management history reference the same immutable artifact.
- [ ] Missing/ambiguous Principal route, recipient, thread, policy or artifact fails closed.
- [ ] No autonomous caller or test/local mailbox mutation exists.

## Outcome
