---
id: PLAT-067
type: ticket
title: Sterilize intake data and deploy release 38
status: preparing
area: platform-operations
assignee: ''
profile: chore
labels:
  - production
  - release
  - intake-wipe
  - requires-live-approval
links: []
deployment: not-deployed
archived: false
created: '2026-09-02T11:55:45.210Z'
updated: '2026-09-02T11:55:45.210Z'
---

## What

Sterilize the production intake-generated Blob and SQL data, then promote and deploy the reviewed release-38 candidate and record the observed production state.

## Why

A clean estate is required before the next intake test round, and the current `dev` candidate contains deployable mail, Web, Infrastructure, and Core changes that must follow the exact-SHA authorised release route.

## Approach

- Inventory, approve, execute, and verify the bounded intake-data wipe.
- Promote and release the frozen exact SHA through immutable artifacts and exact-target approvals.
- Record wipe and release evidence in the canonical current-state documents and promote that documentation without redeploying unchanged code.

## Verification

- [ ] Blob and SQL wipe post-checks pass and the Web UI shows no wiped cases or intake emails.
- [ ] Exact release SHA, artifact digest, Web revision, Worker deployment, smoke, and focused checks pass.
- [ ] `docs/operations.md` and `docs/current-architecture.md` match the observed state on `main`.

## Outcome
