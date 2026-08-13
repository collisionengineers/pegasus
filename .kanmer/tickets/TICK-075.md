---
id: TICK-075
type: ticket
title: >-
  MAIL-17 — Idempotent report/fee-note send on the original Outlook thread or
  provider API using principal CC/delivery/standing-not…
status: todo
area: mail-chasing-outbound-sent-evidence
priority: medium
assignee: ''
labels:
  - capability
  - MAIL-17
  - later
  - post-alpha
  - blocked
  - requires-live-approval
links:
  - TICK-055
archived: false
created: '2026-08-12T15:05:39.996Z'
updated: '2026-08-12T15:09:20.083Z'
---

## What

Plan and research **MAIL-17**: Idempotent report/fee-note send on the original Outlook thread or provider API using principal CC/delivery/standing-note preferences, followed by Box filing, completion, and management-event recording

## Why

This is allocated to **Later / 1.2.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-17.
- Blocked by: [[TICK-055]] — Report/fee-note dispatch must respect the post-report lifecycle.
