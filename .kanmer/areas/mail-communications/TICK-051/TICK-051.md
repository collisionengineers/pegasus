---
id: TICK-051
type: ticket
title: MAIL-09 — Automatic association of related email and attachments with a case
status: done
area: mail-communications
order: 2220
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:44.582Z'
  review: '2026-08-20T19:24:43.526Z'
  verifying: '2026-08-20T20:27:38.048Z'
  done: '2026-08-21T15:12:01.370Z'
labels:
  - capability
  - MAIL-09
  - next
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
blocks:
  - TICK-052
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 33aa2dfb0a1ee26220a81078b0c2fc9ae2a7f63e
  - a940af83995caa811da93b4b439cc12037d7dc48
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/486'
deployment: production
archived: false
created: '2026-08-12T15:05:19.257Z'
updated: '2026-08-25T06:46:19.333Z'
---

## What

Plan and research **MAIL-09**: Automatic association of related email and attachments with a case

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-09.
