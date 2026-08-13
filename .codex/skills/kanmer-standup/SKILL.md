---
name: kanmer-standup
description: Summarise the current state of a project's Kanmer board — what's in progress, what's blocked or stale, what changed recently. Use whenever the user asks for a standup, status update, board summary, progress report, "where are we", "what's left", or wants to groom/triage the backlog in a project with a .kanmer folder.
---

# Kanmer standup / board report

Report facts, not inferences. The activity log records what actually
happened; summaries carry taken/blocked/due state directly. Never report from
memory of the conversation — the human or another agent may have changed the
board since.

## Gather

1. `get_status` — orientation: whether the board is real (`boardSource:
   "file"`) or a synthesized default, per-stage counts, and `warningsCount`.
   If the board doesn't exist or has no items, say so and stop — don't
   report a default board as though it were configured.
2. `list_board` — the configured stages, in order. This list defines **which
   report sections exist and what belongs in each**, not just display names —
   a customised or pre-existing board rarely has the fresh-board defaults, so
   never assume them.
3. `list_items` (`sort: "updated_desc"`) — the current picture. Summaries
   carry everything the report needs: `taken` (who/branch/worktree),
   `blocked` (a live blocker exists), `due`, `checklist` progress,
   `updated`. If the response is `{ items, warnings }`, put the warnings
   under **Flags** — malformed files are exactly what the human needs to see.
4. `get_activity since: <yesterday's ISO timestamp>` — what actually
   happened: moves (`field: "status"` with from/to), takes/releases, doc
   writes, creations, deletions, each with its `actor`. This is what makes
   "API-004 moved to review yesterday (claude)" a fact rather than a guess
   from timestamps.

## Report format

Map the board's stages to sections **by position and role, not by matching
ids against defaults**:

- the **first** stage in `list_board` → **Up next**;
- the **last** stage → **Recently done**;
- a stage whose id or name reads as review/approval → **In review**;
- **every other stage** → **In flight**;
- an item whose `status` isn't on the board at all → **Flags** as an
  "off-board stage" — the signal the human needs, not something to guess a
  bucket for.

Omit empty sections: the report's value is that the user reads it in fifteen
seconds.

### Board: <project folder name>

**In flight** — tickets in the working stages. One line each:
`ID title (stage, area, priority)`, plus the real state: taken tickets show
`⛏ branch` (and worktree) from the summary's `taken`; checklist progress
shows as `3/7`. Mark anything whose `updated` is >7 days old *stale*.

**In review** — tickets in the review stage and who they're waiting on:
`assignee` if set, otherwise unassigned.

**Up next** — the top of the first stage in board order (summaries are
already sorted by the human's manual ordering). 3–5 max.

**Recently done** — what reached the final stage in the last 7 days. Prefer
the activity log (`field: "status"`, `to: <last stage>`) over `updated` —
count plus highlights, with the actor when it wasn't the user.

**Blocked** — every item whose summary says `blocked: true`; name the
blockers from `get_links` (`blockedBy`) when it matters.

**Overdue** — items with `due` before today that aren't in the final stage
(`list_items overdue: true` returns exactly this set).

**What happened since yesterday** — from the activity log: created, moved,
taken/released, docs written — grouped by actor when more than one was
active. Keep it to the notable lines.

**Flags** — anything needing the user: file warnings from `list_items`,
off-board stages, stale items, tickets with no area, tickets taken >3 days
with no activity since.

Keep it scannable — one line per item, no quoting bodies unless asked.
