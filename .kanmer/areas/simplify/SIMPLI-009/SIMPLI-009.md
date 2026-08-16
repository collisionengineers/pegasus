---
id: SIMPLI-009
type: ticket
title: Make Worker the sole processor for queued intake
status: implementing
area: simplify
order: 150
assignee: ''
profile: feature
labels: []
links: []
blocks: []
archived: false
created: '2026-08-13T12:12:48.881Z'
updated: '2026-08-14T11:10:53.985Z'
---

## What

Make the Worker the only component that processes queued intake.

## Why

The current Web and Worker paths compete for ownership, creating permission, durability, and recovery risks.

## Approach

- Stage work as pending and dispatch it through the queue.
- Remove Web inline processing and polling.
- Repair stranded dispatched work and classify unexpected failures explicitly.

## Verification

- [ ] Duplicate delivery, crash-after-stage, lease expiry, poison handling, and Web/Worker permission-boundary tests pass.
