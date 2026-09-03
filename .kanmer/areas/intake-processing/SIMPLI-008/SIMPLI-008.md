---
id: SIMPLI-008
type: ticket
title: Show queued receipt processing status to staff
status: done
area: intake-processing
order: 330
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T10:19:44.061Z'
  verifying: '2026-08-17T11:16:23.469Z'
  done: '2026-08-17T11:55:02.983Z'
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
commits:
  - 195154f9
  - e9f27fe7
  - caad05e8
  - 8bf0a3e6
  - fc144848
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/385'
deployment: not-deployed
archived: false
created: '2026-08-13T12:12:48.862Z'
updated: '2026-09-03T09:06:43.857Z'
---

## What

Provide a receipt-keyed staff view for queued intake processing.

## Why

After upload, staff need a visible outcome rather than an unidentifiable queued response.

## Approach

- Show Received, Processing, Complete, or Failed.
- Link to the resulting case or recovery view.

## Verification

- [x] A queued upload exposes its current state and destination to staff — see `proof`.

## Outcome

Shipped in PR #385 (https://github.com/collisionengineers/pegasus/pull/385), merged to `dev` as `fc144848` on 2026-08-17; not deployed. Delivered together with [[SIMPLI-009]] on `task/simpli-009`: authorised `/Upload/Status/{id}` on the design system with Received / Processing / Complete / Failed, CSP-safe auto-refresh while nonterminal, 404 for unknown ids, "Open case" / "Open receipt" links, and a replay notice carried as `?duplicate=true`.

Shipped differently than planned: the duplicate notice moved from one-shot `TempData` (which vanished on the first auto-refresh) to the existing `?duplicate=` route-value convention; the page was rebuilt on the shared CSS classes after review.

Follow-up: [[INTK-001]] — `retry_scheduled` reads as Received and polls every 2 s for the retry's duration; auto-associated receipts link to the receipt rather than the case.
