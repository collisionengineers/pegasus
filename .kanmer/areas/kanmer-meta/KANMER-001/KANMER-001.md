---
id: KANMER-001
type: ticket
title: Retarget Kanmer tickets that cite the retired NOW.md / requirements.md
status: todo
area: kanmer-meta
priority: medium
assignee: ''
labels: []
links:
  - SIMPLI-004
archived: false
created: '2026-08-14T11:15:00.271Z'
updated: '2026-08-14T11:15:00.271Z'
---

## What

After PR #374 retires `NOW.md` and `docs/requirements.md`, a target-revision scan
finds ~131 `todo` tickets and ~5 `in-progress` tickets that still cite one of
those deleted files as authoritative (e.g. TICK-111 calls `NOW.md` authoritative;
TICK-023 links its canonical owner through `requirements.md`). Agents reading
those tickets get stale queue/authority context.

## Why deferred

Operator direction (PR #374 review reply): **hold** — a Kanmer update is coming
that will be valuable for doing this migration well. Do not mass-edit the board
before then.

## Approach (when carried out)

- Retarget each ticket's authority/owner reference from `NOW.md` →
  the Kanmer board / `operations.md` / `open-decisions.md`, and from
  `requirements.md` → the owning PRD (`docs/prd/`) or FRD (`docs/frd/`) section
  (heading slugs were preserved, so anchors map cleanly).
- Archive tickets that only restated NOW.md queue lines and are no longer actionable.

Coordinates with [[SIMPLI-004]] (NOW.md retirement) and [[SIMPLI-005]] (board triage).
