---
id: PLAT-067
type: ticket
title: Sterilize intake data and deploy release 38
status: done
area: platform-operations
assignee: codex-root
profile: chore
stageEntered:
  review: '2026-09-02T13:37:06.763Z'
  verifying: '2026-09-02T13:37:16.258Z'
  done: '2026-09-02T13:37:23.969Z'
labels:
  - production
  - release
  - intake-wipe
  - requires-live-approval
groups:
  - HZN-004
links: []
commits:
  - 0f0e90ae44ffda7339ca2a460310deeb98121afa
  - 70bb9653b598f7944b46c6be0deae36fa09064ba
  - 1b705bd01d88109b21affddd014fbaa06c82b1ce
prs:
  - '645'
deployment: production
archived: false
created: '2026-09-02T11:55:45.210Z'
updated: '2026-09-02T13:37:34.385Z'
---

## What

Sterilize the production intake-generated Blob and SQL data, then promote and deploy the reviewed release-38 candidate and record the observed production state.

## Why

A clean estate was required before the next intake test round, and the `dev` candidate contained deployable mail, Web, Infrastructure, and Core changes that required the exact-SHA authorised release route.

## Approach

- Inventory, approve, execute, and verify the bounded intake-data wipe.
- Promote and release the frozen exact SHA through immutable artifacts and exact-target approvals.
- Record wipe and release evidence in the canonical current-state documents and promote that documentation without redeploying unchanged code.

## Verification

- [x] Blob and SQL wipe post-checks passed and the operator confirmed the wiped data was absent from the authenticated Web UI.
- [x] Exact release SHA, artifact digest, Web revision, Worker deployment, smoke, and focused checks passed.
- [x] `docs/operations.md` and `docs/current-architecture.md` match the observed state on `main`.

## Outcome

Release 38 is production-verified. The sparse Graph entry no longer blocks the cursor, queued emails are arriving, and the release evidence is promoted to `main` at `1b705bd01d88109b21affddd014fbaa06c82b1ce`.
