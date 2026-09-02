---
id: TICK-033
type: ticket
title: >-
  INT-31 — Authenticated staff generate a temporary, revocable, expiring,
  request-scoped link for isolated unauthenticated image/d…
status: done
area: intake-processing
order: 170
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-18T14:12:27.399Z'
  review: '2026-08-18T15:28:37.025Z'
  verifying: '2026-08-18T15:38:33.639Z'
  done: '2026-08-20T03:21:57.933Z'
labels:
  - capability
  - INT-31
  - now
  - requires-live-approval
groups:
  - HZN-003
links: []
blocks: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - b32532d888b676bffaf197675b3e0edded5f0e81
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/408'
deployment: production
archived: false
created: '2026-08-12T15:03:53.456Z'
updated: '2026-09-01T14:44:31.805Z'
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
- Canonical owner: [Request-scoped upload links](docs/frd/frd-02-intake-and-source-identity.md#request-scoped-upload-links)
- Activation/boundary: Allocated but non-blocking for `0.1.0-alpha.1` acceptance. Token, limit, custody, retry, revocation, abuse, and cross-request isolation contracts are acceptance gates for the capability itself; supersedes Box File Request (UI removal pending).
