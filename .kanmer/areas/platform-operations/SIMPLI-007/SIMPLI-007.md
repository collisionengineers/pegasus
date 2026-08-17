---
id: SIMPLI-007
type: ticket
title: Move the QDOS alpha acceptance gate out of application composition
status: implementing
area: platform-operations
order: 130
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
archived: false
created: '2026-08-13T12:12:48.841Z'
updated: '2026-08-17T06:40:47.033Z'
---

## What

Remove the QDOS alpha acceptance gate from Core and Web composition while retaining useful release validation in tooling.

## Why

A test-only manifest checker is currently part of the running application and carries obsolete release requirements.

## Approach

- Move the validator to release tooling.
- Remove the unused application-facing gate and interface.

## Verification

- [ ] Application composition no longer registers the acceptance gate and release validation remains available.
