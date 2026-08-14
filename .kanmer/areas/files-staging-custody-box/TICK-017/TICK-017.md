---
id: TICK-017
type: ticket
title: DOC-01 — Automatic Box case-folder creation using the Case/PO name
status: review
area: files-staging-custody-box
priority: medium
order: 40
assignee: claude-code
taken_at: '2026-08-13T20:09:42.224Z'
branch: task/int-25-doc-01-planning
worktree: 'C:\Users\PC\Documents\GitHub\pegasus-worktrees\int-25-doc-01-planning'
labels:
  - capability
  - DOC-01
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.105Z'
updated: '2026-08-13T21:23:10.884Z'
---

## What

Plan and research **DOC-01**: Automatic Box case-folder creation using the Case/PO name

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — DOC-01.
- Canonical owner: [Requirements](requirements.md#documents-extraction-and-custody)
- Activation/boundary: Immutable Case/PO naming, response-loss-safe binding, fail-closed conflict handling and human reasoned recovery are caller-proved locally. Live controlled Box target proof, migration, deployment and operator acceptance remain pending.
