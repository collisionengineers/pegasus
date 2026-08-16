---
id: SIMPLI-010
type: ticket
title: Consolidate intake state around the receipt-to-case link
status: implementing
area: simplify
order: 160
assignee: ''
profile: feature
labels: []
links: []
blocks: []
archived: false
created: '2026-08-13T12:12:48.901Z'
updated: '2026-08-14T11:10:54.132Z'
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
