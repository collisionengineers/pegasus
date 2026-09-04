---
id: PLAT-068
type: ticket
title: Sign-off Engineer account setting with qualifications and signature image
status: done
area: platform-operations
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-09-02T20:53:21.795Z'
  review: '2026-09-03T21:09:35.151Z'
  verifying: '2026-09-03T23:36:17.640Z'
  done: '2026-09-04T07:13:27.334Z'
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
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-12-operator-experience.md
commits:
  - 6d1a4fe2
  - 9fb10d5d
  - 82c93d8b
  - 5036d546
  - a6f4bfe1
  - a1f5b947c85ceee6ceef14a0318eb4dcdd49ac19
prs:
  - '655'
delivery_state: integrated
delivery_branch: dev
delivery_sha: 3f0cb45edf5eef0e9cd592b7e7305aaea8e96c44
delivery_recorded_at: '2026-09-03T23:36:18.976Z'
archived: false
created: '2026-09-02T20:31:38.788Z'
updated: '2026-09-04T07:15:00.058Z'
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

Added the Sign-off Engineer account setting (flag, printed name, qualifications, PNG signature, one Default sign-off Engineer designation) with the Core `SignOffEngineerProfile` seam for DOCS-017/CASE-040, shipped in PR #655, merged to `dev` at `3f0cb45edf5eef0e9cd592b7e7305aaea8e96c44`.
