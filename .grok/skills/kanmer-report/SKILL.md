---
name: kanmer-report
description: Report a Kanmer board's state or history — a standup (now — in flight/blocked/up next) or a retro (since <period> — what shipped, throughput). Use for a status update, standup, board summary, "where are we", "what's left", or a look-back like "what shipped last month". DO NOT USE for fixing what it flags (kanmer-groom).
---

# Kanmer report

One read-only reporting skill, two modes over the same four read tools — they
differ only by time window. Report **facts, not inferences**: the activity log
records what happened; summaries carry taken/blocked/checklist state directly.
Never report from memory of the conversation — the board may have changed since.

## Workflow

1. **Pick the mode** from what was asked — "now" (standup) or "since \<period\>"
   (retro). Everything after this differs only by time window.
2. **Gather** the four reads below, in that order.
3. **Write the sections** for that mode, omitting empty ones.
4. **Stop.** This skill changes nothing — no stage moves, no tickets, no
   documents. It operates across every stage and belongs to none.

## Gather (both modes)
1. `get_status` — is the board real (`boardSource: "file"`) or a synthesized
   default? Per-stage counts, `warningsCount`. If it doesn't exist or has no
   items, say so and stop — don't report a default board as configured.
2. `list_board` — the configured stages **in order**. This defines which
   sections exist and what belongs in each; never assume the fresh-board defaults.
3. `list_items` (`sort: "updated_desc"`) — the current picture: `taken`
   (who/branch/worktree), `blocked`, `checklist` progress, `deployment`,
   `updated`. If the response is `{ items, warnings }`, put the warnings under **Flags**.
4. `get_activity` with the window's `since`.

## Mode: now (standup)
Map stages by **position and role**, not by matching ids against defaults:
first stage → **Up next**, last → **Recently done**, a review-like stage → **In
review**, every other → **In flight**; a status not on the board → **Flags** as
an off-board stage. Sections (omit empty ones):

- **In flight** — working stages; one line each, `⛏ branch` for taken, `3/7` checklist.
- **In review** — who each is waiting on.
- **Up next** — top of the first stage, 3–5 max.
- **Recently done** — reached the final stage in the last 7 days (prefer the activity log's `to: <last stage>`), with the actor when it wasn't the user.
- **Blocked** — every `blocked: true`; name blockers from `get_links`.
- **What happened since yesterday** — created / moved / taken / docs written, grouped by actor.
- **Flags** — file warnings, stale (>7d) items, **doc-gate debt** (`docs_todo:true` still unlinked, or a required doc missing — from `get_doc_gates`), tickets taken >3d with no activity.

## Mode: since \<period\> (retro)
`get_activity since: <period start>`. Report what reached the final stage
(count + highlights, actor when not the user), throughput per stage, what's
still open from that period, and recurring Flags. A look-back, not a to-do list.

Keep it scannable — one line per item, no quoting bodies unless asked.

---

**No hand-off — control returns to whoever asked.** A report is read-only by
design, so it ends where it started. What it *flags* has owners: board problems
go to `kanmer-groom` (which fixes what this skill only names), doc-gate debt to
`kanmer-docs`, and a specific ticket to its phase skill. Naming a fix is this
skill's job; applying one is not.
