---
id: KANMER-001
type: ticket
title: Retarget Kanmer tickets that cite the retired NOW.md / requirements.md
status: done
area: kanmer-meta
order: 490
assignee: codex
profile: chore
stageEntered:
  implementing: '2026-08-17T04:08:18.711Z'
  review: '2026-08-17T04:10:50.516Z'
  verifying: '2026-08-17T04:14:08.436Z'
  done: '2026-08-17T04:16:09.166Z'
labels: []
groups:
  - HZN-001
links:
  - SIMPLI-004
commits:
  - 0f793a28285b90ab072bf095990a1561c48ba4e6
deployment: n/a
archived: false
created: '2026-08-14T11:15:00.271Z'
updated: '2026-08-26T14:34:43.417Z'
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

## Outcome

Completed 2026-08-17 under [[HZN-001]]. Retargeted all 157 affected ordinary ticket bodies, archived 77 non-actionable mechanical imports, preserved substantive CI and renderer work, and linked TICK-203–TICK-216 to [[SIMPLI-015]]. Independent re-review passed. Verified committed board state is recorded in proof.md. No application deployment or PR applied.
