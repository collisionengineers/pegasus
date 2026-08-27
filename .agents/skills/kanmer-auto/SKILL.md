---
name: kanmer-auto
description: Autonomously clear a set of Kanmer tickets — target an area or a group (epic/horizon), and drive every eligible ticket through its profile's pipeline up to a specified point, orchestrating parallel subagents in conflict-free waves and respecting the document gates. Use when the user says "clear the API area", "work through the backlog", "burn down UI up to review", "do all the tickets in <area>", "clear HZN-003", "work through 0.3.3", "finish this epic". DO NOT USE FOR a single ticket — use the phase skills directly.
---

# Clearing an area autonomously

kanmer-auto is orchestration, not new mechanics: each ticket still goes
through the phase skills' procedures exactly as written — this skill decides
which tickets, in what order, and how many at once.

## 1. Gather and scope

- `get_status`, then `list_items` for the target — `area` for a subsystem,
  `group` for an epic or a horizon (`list_items group: "HZN-003"`). Board order
  is the human's ordering — respect it. Group membership is derived in id order
  and is *not* a priority, so a group scopes the roster and nothing else.
  Filters are AND, so `group` + `area` narrows to one subsystem's share of a
  release.
  Use `list_items`, not `get_group`, to build the roster: `get_group`'s derived
  members carry only id/title/stage, and the drop rules below need `taken` and
  `blocked`, while §3 needs `profile`.
- `get_group_doc` for the group's shared context if you scoped by one — the
  constraint binding the batch is written there once and applies to every
  member.
- Drop: archived, `blocked: true`, and tickets taken by someone else
  (coordinate, don't `force`).
- Parse the **target point** from the request: "up to review" means each
  ticket stops once its PR is open and the ticket sits in the review stage;
  the default is full closeout (merge permitting — if merging is the human's
  call, tickets park in review and you say so). Resolve stage names against
  `list_board`.
- **Gates are hard, and per-ticket.** Call `get_doc_gates <id>` for every ticket
  in the roster and drive *that* ticket's boundaries — do not assume a common
  pipeline. Profiles differ in how many stages they walk and which documents
  they owe, so `reachable` on that call is the roster's routing table. Driving
  every ticket through one pipeline is the mistake this warning exists to
  prevent.
- **One gated boundary per move.** A lane advances a ticket one stage at a time;
  a move crossing two gated boundaries is refused. Partition the roster by
  profile so lanes with genuinely different pipeline lengths do not block each
  other.
- Set `docs_todo` on tickets that need a governing doc written so they are not
  stranded at the first gate.
- Tell the user the roster before starting: what you scoped by (naming the
  group, if you used one), which tickets, target point, what was skipped and
  why. A roster resolved from a group is worth showing back before anything
  starts — it is the one step the user cannot check by reading the request.

## 2. Wave 0 — research everything in parallel

Research is read-only: no branches, no worktrees, no conflicts.
Run one subagent per ticket concurrently (use your host's subagent/task
mechanism), each following the `kanmer-research` skill for its ticket.
Tickets whose research surfaces user-only questions get parked and reported
— don't guess on the user's behalf.

This is not only a wave-0 concern: a question can surface at any point, and a
lane that hits one **stops there and is reported as parked-on-a-question, named
and quoted** — never rolled into the generic failure bucket. The operator can
answer a question in seconds; they cannot answer one they were never shown. The
gates enforce *some* of the stopping — `get_doc_gates` says which boundaries
this ticket's profile actually has, and they are not the same for every profile —
but the merge is outside the engine entirely, so a lane can land code on a
question the operator never saw. Reporting it is therefore this skill's job, not
the engine's.

## 3. Partition into conflict-free lanes

Compare the file tables in each ticket's `files` document:

- Tickets touching **disjoint** files → different lanes, safe in parallel.
- Tickets with **overlapping** files → the same lane, run serially.
- A `blocks` edge forces ordering regardless of lanes: the blocker finishes
  (to the target point) before the blocked ticket starts.

Cap concurrency at ~3 lanes — enough to matter, few enough that rebases and
reviews stay manageable.

## 4. Execute the waves

Every lane works in its **own** worktree, and none of them touches
`.worktrees/kanmer` — in a repo set up through the GUI that is the board's own
worktree, on the board branch, with MCP rooted in it. It is never a lane's
worktree, never a rebase target, and never cleaned up. With ~3 lanes running git
surgery in parallel this is the invariant with the most chances to be broken.

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
**parked** (user-only questions — **quote them**, with the ticket id and the
recommendation, so the operator can answer inline), **skipped**
(blocked / taken / failed, with reasons). Every ticket in the roster
appears in exactly one list — silent drops are how autonomous runs lose
trust.

---

**No single successor — this skill *is* the hand-off.** It drives the phase
skills in order for each ticket in its roster:

    kanmer-research → -plan → -execute → -review → -verify → -closeout

stopping each ticket at the requested target point, and stopping the whole run
at any question only the operator can answer. When the roster is exhausted,
control returns to the operator with the four-list report above.
