---
name: kanmer-standup
description: Summarise the current state of a project's Kanmer board — what's in progress, what's blocked or stale, what changed recently. Use whenever the user asks for a standup, status update, board summary, progress report, "where are we", "what's left", or wants to groom/triage the backlog in a project with a .kanmer folder.
---

# Kanmer standup / board report

Produce a status report from the live board, not from memory of the
conversation — the human or another agent may have changed items since, and
confidently reporting stale state is worse than not reporting.

## Gather

1. `list_board` for the stage and area names, so you can report human-readable
   names rather than ids.
2. `list_items` for all active items. Summaries include `updated`, which is what
   staleness is judged from (an ISO timestamp in the item's frontmatter, moved
   only by real changes — not a file mtime).
3. `get_item` on anything that looks stale, blocked or surprising, so you
   describe its actual state rather than inferring from the title.
4. Only if you intend to report link-based flags (plans whose tickets are all
   done, research nothing points at): `get_links` on those plans and notes.
   Skip this if you're just giving a quick status.

If `list_items` comes back empty, don't report a default board as though it were
configured — say the board has no items yet, mention that the stages shown are
defaults, and stop.

## Report format

Use the structure below, but **only the sections that have something in them**.
Omitting empty sections matters: the value of this report is that the user reads
it in fifteen seconds and knows where to look, and a skeleton of "none" headings
buries that.

The stage sections describe **tickets**. Plans and research notes park in `todo`
by convention rather than being worked through the stages, so they aren't "up
next" — surface them only where they need attention (see Flags).

### Board: <project folder name>

**In flight** — tickets in planning, implementing or verifying (review has its
own section, so it isn't counted here). One line each: `ID title (stage, area,
priority)` — drop the area slot if the ticket has none — plus a few words of real
state. Mark anything whose `updated` is more than 7 days old as *stale*.

**In review** — tickets in review and who they're waiting on: the `assignee` if
set, otherwise say it's unassigned.

**Up next** — the tickets at the top of the todo column, highest priority first.
3–5 max.

**Recently done** — done items whose `updated` falls in the last 7 days. Give a
count plus the highlights, not the full list.

**Flags** — anything needing the user: stale items, tickets with no area, plans
whose tickets are all done (suggest closing), research nothing links to.

Keep it scannable — one line per item, and no quoting bodies unless asked.

## Grooming (only when asked)

If the user asks to groom or triage, propose the batch first — archive stale
done items, fill in missing areas or priorities, reprioritise — and only then
apply the approved subset with `update_item` / `move_item`. Board changes are
visible to everyone looking at it, so a silent bulk edit is disorienting.
