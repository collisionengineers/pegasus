---
id: TICK-033
type: ticket
title: >-
  INT-31 — Authenticated staff generate a temporary, revocable, expiring,
  request-scoped link for isolated unauthenticated image/d…
status: implementing
area: intake-manual-upload-source-intake
order: 50
assignee: ''
profile: feature
labels:
  - capability
  - INT-31
  - now
  - requires-live-approval
links: []
blocks: []
archived: false
created: '2026-08-12T15:03:53.456Z'
updated: '2026-08-14T11:10:52.620Z'
---

## What

Plan and research **INT-31**: Authenticated staff generate a temporary, revocable, expiring, request-scoped link for isolated unauthenticated image/document upload; it exposes only the upload form and immediate result, never case/reference/request state or another document

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — INT-31.
- Canonical owner: [Request-scoped upload links](requirements.md#request-scoped-upload-links)
- Activation/boundary: Allocated but non-blocking for `0.1.0-alpha.1` acceptance. Token, limit, custody, retry, revocation, abuse, and cross-request isolation contracts are acceptance gates for the capability itself; supersedes Box File Request (UI removal pending).
