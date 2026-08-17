---
id: SIMPLI-010
type: ticket
title: Consolidate intake state around the receipt-to-case link
status: implementing
area: intake-processing
order: 160
assignee: claude-code
profile: feature
taken_at: '2026-08-17T10:00:11.412Z'
branch: task/simpli-010-intake-state
worktree: ../pegasus-worktrees/simpli-010-intake-state
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
archived: false
created: '2026-08-13T12:12:48.901Z'
updated: '2026-08-17T10:00:11.412Z'
---

## What

Make the receipt-to-case link the authoritative proof of case creation and remove competing state only when safe.

## Why

Decision codes, processing states, and compatibility paths currently duplicate case-creation truth.

## Approach

- Normalize the remaining draft_ready compatibility path.
- Consolidate state only after production-data inspection.

## Verification

- [ ] Case existence and retry/recovery state have one authoritative source.
