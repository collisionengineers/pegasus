---
id: PLAT-068
type: ticket
title: Sign-off Engineer account setting with qualifications and signature image
status: preparing
area: platform-operations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T20:53:21.795Z'
labels:
  - administration
  - accounts
  - sign-off
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - CASE-040
  - ENG-029
docs_todo: true
archived: false
created: '2026-09-02T20:31:38.788Z'
updated: '2026-09-02T20:53:21.795Z'
---

## What

Staff accounts in the Engineer role gain a Sign-off Engineer setting with qualifications and a stored signature image; the accounts table shows it; only flagged accounts are offered as sign-off.

## Why

D31; three signatures exist (Andy, Neil, Ed) and not every Engineer signs. Andy is the default; Neil's qualifications are recorded later by an Administrator. Mockup source: `Pegasus_UI_v2_src/src/17-admin.js` accounts dialog.

## Approach

- Extend the PLAT-027 account settings dialog; reuse the brand signature assets; one migration with grants.

## Verification

- [ ] Administrator-only, reasoned, recorded in Action Logs.
- [ ] Renderer reads the sign-off tuple (DOCS-017).

## Outcome
