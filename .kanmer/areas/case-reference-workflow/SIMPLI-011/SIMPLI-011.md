---
id: SIMPLI-011
type: ticket
title: Decompose the Case Details workspace by capability
status: implementing
area: case-reference-workflow
order: 170
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-002
links: []
blocks: []
archived: false
created: '2026-08-13T12:12:48.922Z'
updated: '2026-08-17T06:40:47.767Z'
---

## What

Keep one Case workspace while moving mutations into capability-specific Razor endpoints.

## Why

The existing page has excessive handlers and dependencies, making safe changes difficult.

## Approach

- Separate workflow, tasks, custody, vehicle/EVA, and closure operations.
- Keep DetailsModel focused on loading and displaying the workspace.

## Verification

- [ ] The visible workspace remains intact and extracted operations are covered by behavioural tests.
