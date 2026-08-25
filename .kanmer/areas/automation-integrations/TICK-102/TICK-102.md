---
id: TICK-102
type: ticket
title: Activate AI-09 after accepting a non-preview production transport
status: backlog
area: automation-integrations
order: 10
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T05:39:28.583Z'
  implementing: '2026-08-20T05:40:03.231Z'
  review: '2026-08-20T05:40:10.234Z'
  verifying: '2026-08-20T05:46:39.396Z'
  backlog: '2026-08-25T06:42:09.116Z'
labels:
  - capability
  - AI-09
  - now
  - requires-live-approval
groups:
  - EPIC-005
  - HZN-003
links: []
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0021-automation-actor-direct-write-assessment-contract.md
archived: false
created: '2026-08-12T15:06:02.768Z'
updated: '2026-08-25T06:42:09.116Z'
---

## What

Activate AI-09 only after Collision Engineers accepts a non-preview production transport for the existing durable Send-to-AI work request.

## Why

The durable request, immutable case/version stamp, pointer-only hand-off, idempotency, attributed unconfirmed writes, and delivery states already exist under ADR-0021. They are not a live capability: `Features:SendToAi` is absent from production configuration, and the current research-preview channel is coded to fail closed outside `DevelopmentOffline`.

A closed composition gate is disabled behavior, not a delivered feature. This ticket therefore returns to Backlog until the separate non-preview transport decision named by `docs/capabilities.md` is accepted.

## Activation boundary

- Accept the production transport and its security, identity, failure, and recovery behavior before changing the gate.
- Reuse the existing AI-09 Core request and review contracts; do not keep the preview transport as a fallback.
- Compose the accepted transport in production and remove or replace the DevelopmentOffline-only restriction as the decision requires.
- Keep the surface absent and fail closed until the complete production route is supportable.

## Verification

- [ ] The non-preview production transport decision is accepted and linked.
- [ ] A real production Send-to-AI round trip uses the durable pointer-only request and records visible delivery status.
- [ ] Duplicate, expired, cancelled, or failed requests cannot mutate accepted data.
- [ ] Returned Automation Actor writes remain attributed and unconfirmed until the accepted staff review point.
- [ ] Production failure and recovery evidence is recorded before the capability advances to Done.

## Existing evidence

The ticket's research, plan, checklist, and post-implementation report remain as evidence of the implemented-but-closed preview path; they are not evidence of live activation.
