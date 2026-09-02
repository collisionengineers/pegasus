---
id: MAIL-030
type: ticket
title: Administer staff-initiated outbound delivery policy and activity
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-011
links:
  - MAIL-024
  - MAIL-026
  - MAIL-027
  - TICK-075
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/frd/frd-12-operator-experience.md
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.000Z'
updated: '2026-09-01T14:40:45.000Z'
---

## What

Add Administration controls for the approved default outbound mailbox, staff review/approval modes for report and fee-note delivery, and retained delivery activity with review and controlled retry.

## Why

The prototype's non-autonomous delivery settings have no single production owner. MAIL-026/027 and MAIL-17 own the underlying staff-driven correspondence behavior, not its administration surface and activity policy.

## Approach

- Reuse the approved-mailbox identity, staff-initiated composer, Sent-evidence and external-work conventions.
- Keep delivery policy Principal/mailbox scoped where the governing FRDs require it.
- Show real retained activity and retry only a typed retryable failure.
- Do not add autonomous correspondence; remove that prototype control from the production reconciliation.

## Verification

- [ ] Every setting changes a real Core-owned policy and has permanent attributable history.
- [ ] Review/approved modes cannot bypass staff authorization or report readiness.
- [ ] Retry is idempotent and never duplicates a successful delivery.
- [ ] No local or test profile mutates Outlook.

## Outcome
