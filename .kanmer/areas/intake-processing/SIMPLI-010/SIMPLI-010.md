---
id: SIMPLI-010
type: ticket
title: Consolidate intake state around the receipt-to-case link
status: done
area: intake-processing
order: 140
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T12:01:25.308Z'
  verifying: '2026-08-17T12:10:42.476Z'
  done: '2026-08-17T12:25:13.502Z'
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
commits:
  - 1e5372ce
  - 5e59f933
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/387'
deployment: not-deployed
archived: false
created: '2026-08-13T12:12:48.901Z'
updated: '2026-08-19T09:39:14.680Z'
---

## What

Make the receipt-to-case link the authoritative proof of case creation and remove competing state only when safe.

## Why

Decision codes, processing states, and compatibility paths currently duplicate case-creation truth.

## Approach

- Normalize the remaining draft_ready compatibility path.
- Consolidate state only after production-data inspection.

## Verification

- [x] Case existence and retry/recovery state have one authoritative source — see `proof`.

## Outcome

Shipped in PR #387 (https://github.com/collisionengineers/pegasus/pull/387), merged to `dev` as `5e59f933` on 2026-08-17; not deployed. The `draft_ready` alias is gone from persistence, Operations, docs and UI vocabulary; `case_created` is the sole persisted code and is not case-existence authority — the Case intake link is.

Shipped differently than planned: "consolidate only after production-data inspection" was satisfied by a **read-only production count** (0 `draft_ready` rows, 0 unleased `dispatched` work items) instead of the 13-step normalisation/migration plan; the plan was cut to six steps. The stale-`dispatched` re-dispatch that the SIMPLI-009 review routed here was not implemented (nothing to repair) and is [[INTK-003]].

Follow-ups: [[INTK-002]] (one decision-code table across `ParseDecision` / `MapIntakeState` / MCP), [[INTK-003]] (stale `dispatched` recovery), [[INTK-004]] (decision-label and Operations case-link doc/code reconciliation).
