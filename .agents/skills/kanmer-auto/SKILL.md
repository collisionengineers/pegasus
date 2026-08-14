---
name: kanmer-auto
description: Autonomously clear a set of Kanmer tickets — target an area (or filter) and drive every eligible ticket through research → plan → execute → review → verify → closeout up to a specified point, orchestrating parallel subagents in conflict-free waves and respecting the document gates. Use when the user says "clear the API area", "work through the backlog", "burn down UI up to review", "do all the tickets in <area>". DO NOT USE FOR a single ticket — use the phase skills directly.
---

# Clearing an area autonomously

kanmer-auto is orchestration, not new mechanics: each ticket still goes
through the phase skills' procedures exactly as written — this skill decides
which tickets, in what order, and how many at once.

## 1. Gather and scope

- `get_status`, then `list_items` for the target area (board order is the
  human's priority order — respect it).
- Drop: archived, `blocked: true`, and tickets taken by someone else
  (coordinate, don't `force`).
- Parse the **target point** from the request: "up to review" means each
  ticket stops once its PR is open and the ticket sits in the review stage;
  the default is full closeout (merge permitting — if merging is the human's
  call, tickets park in review and you say so). Resolve stage names against
  `list_board`.
- **Gates are hard.** Every lane obeys the document gates (`get_doc_gates`): it
  can't leave Backlog without a governing doc (`link_doc`/`docs_todo`), leave
  Planning without plan+checklist, enter Review without the post-implementation
  report, or reach Done without proof. Set `docs_todo` on tickets that need a
  governing doc written so they aren't stranded at the first gate.
- Tell the user the roster before starting: which tickets, target point,
  what was skipped and why.

## 2. Wave 0 — research everything in parallel

Research and impact are read-only: no branches, no worktrees, no conflicts.
Run one subagent per ticket concurrently (use your host's subagent/task
mechanism), each following the `kanmer-research` skill for its ticket.
Tickets whose research surfaces user-only questions get parked and reported
— don't guess on the user's behalf.

## 3. Partition into conflict-free lanes

Compare the impact.md file tables across tickets:

- Tickets touching **disjoint** files → different lanes, safe in parallel.
- Tickets with **overlapping** files → the same lane, run serially.
- A `blocks` edge forces ordering regardless of lanes: the blocker finishes
  (to the target point) before the blocked ticket starts.

Cap concurrency at ~3 lanes — enough to matter, few enough that rebases and
reviews stay manageable.

## 4. Execute the waves

Each lane's current ticket runs in its own subagent: `kanmer-plan` →
`kanmer-execute` (own worktree `.worktrees/<id>`, own branch) →
`kanmer-review` (which merges on pass) → `kanmer-verify` (validate on merged
main, write proof) → `kanmer-closeout` — each phase only as far as the target
point allows. After anything merges to main, lanes still in flight rebase
before opening their PRs:

```sh
git fetch origin && git rebase origin/main
```

A ticket that fails (tests won't pass, plan turns out wrong, rebase
conflicts beyond mechanical resolution) doesn't sink the run: release it,
append what happened to its checklist progress notes, move it back to the
appropriate stage, and continue the lane with the next ticket.

If your host has no subagent mechanism, run the same waves sequentially —
the lane partition still tells you the safe order.

## 5. Report

Finish with a standup-style summary: **cleared** (closed out),
**at target** (parked at the requested point, e.g. awaiting merge),
**parked** (user-only questions, with the questions), **skipped**
(blocked / taken / failed, with reasons). Every ticket in the roster
appears in exactly one list — silent drops are how autonomous runs lose
trust.
