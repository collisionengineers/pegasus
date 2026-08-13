---
id: SIMPLI-007
type: ticket
title: Move the QDOS alpha acceptance gate out of application composition
status: todo
area: simplify
priority: medium
assignee: ''
labels: []
links: []
archived: false
created: '2026-08-13T12:12:48.841Z'
updated: '2026-08-13T12:12:48.841Z'
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
