---
id: TICK-049
type: ticket
title: MAIL-07 — Move the confirmed message to the designated Outlook folder
status: todo
area: ui-emails
priority: medium
assignee: ''
labels:
  - capability
  - MAIL-07
  - next
  - post-alpha
  - blocked
  - requires-live-approval
links:
  - TICK-048
archived: false
created: '2026-08-12T15:05:19.217Z'
updated: '2026-08-12T15:09:19.904Z'
---

## What

Plan and research **MAIL-07**: Move the confirmed message to the designated Outlook folder

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-07.
- Blocked by: [[TICK-048]] — A folder move may occur only after staff confirmation.
