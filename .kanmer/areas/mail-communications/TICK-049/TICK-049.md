---
id: TICK-049
type: ticket
title: MAIL-07 — Move the confirmed message to the designated Outlook folder
status: done
area: mail-communications
order: 780
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:37.436Z'
  review: '2026-08-20T14:53:19.600Z'
  verifying: '2026-08-20T17:50:48.891Z'
  done: '2026-08-21T15:11:06.893Z'
labels:
  - capability
  - MAIL-07
  - next
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links:
  - TICK-048
blocks:
  - TICK-050
  - TICK-054
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 8b1e6d74
  - f60248af4a078c1fa188a46143818d2cce2683c9
  - 5e8217a1d3f23caf7a137b24cdc79366175c35c8
  - fc3b651eda785ad37fbe7c302aec38e2876abc20
  - 83293162c3059d52b05d5139e2d1b8ee56b8d5a9
  - 1cc0927d22bc4976ecb4e8b5491658a9db3eedd3
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/477'
deployment: production
archived: false
created: '2026-08-12T15:05:19.217Z'
updated: '2026-08-25T06:46:14.211Z'
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
