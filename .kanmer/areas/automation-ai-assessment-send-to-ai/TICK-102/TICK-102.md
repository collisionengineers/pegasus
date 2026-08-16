---
id: TICK-102
type: ticket
title: >-
  AI-09 — Staff Send to AI creates one durable idempotent capability-scoped work
  request bound to an immutable case/version stamp…
status: backlog
area: automation-ai-assessment-send-to-ai
assignee: ''
profile: feature
labels:
  - capability
  - AI-09
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:06:02.768Z'
updated: '2026-08-12T15:06:02.768Z'
---

## What

Plan and research **AI-09**: Staff `Send to AI` creates one durable idempotent capability-scoped work request bound to an immutable case/version stamp; the hand-off carries a pointer only, and the scoped worker returns its work as attributed unconfirmed Automation Actor writes reviewed at manual engineer assignment, with delivery status and visible failure on the tracking record

## Why

This is allocated to **Now / 0.1.0-alpha.1** in `docs/capabilities.md`. It is a current allocated outcome with remaining caller/evidence work.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — AI-09.
