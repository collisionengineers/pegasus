---
name: kanmer-tickets
description: Manage tickets on Kanmer, the file-based kanban shared live with a human's board GUI — orient on the board, create, update, link, order and archive tickets, and keep the board's conventions. Use whenever the user asks to add or update a ticket, manage the backlog, or asks "what's on the board" / "add a ticket for this" — and also proactively when you start a multi-step piece of work in a project that contains a .kanmer folder, so the work is filed before it starts and the human can follow it. DO NOT USE FOR doing the work itself — researching a ticket (kanmer-research), planning it (kanmer-plan), implementing it (kanmer-execute), reviewing it (kanmer-review), post-merge cleanup (kanmer-closeout), or clearing a whole area (kanmer-auto).
---

# Managing Kanmer tickets

Kanmer stores tickets as folders in the project's `.kanmer/` tree — each
ticket owns its markdown file plus a pipeline of documents (research, files,
plan, checklist, proof) that live beside it. The human sees the same data on a
live kanban board (the Kanmer desktop app), so every item you create or move
is instantly visible to them — treat the board as your shared workspace, not
a log.

All access goes through the `kanmer` MCP tools. Never edit `.kanmer/` files
directly; the tools keep ids, timestamps, folders and frontmatter consistent.

This skill covers the tickets themselves. The work *inside* a ticket is
driven by the phase skills — see the router at the end.

## Workflow

1. **Orient** — `get_status`, then `list_board`, then `list_items`.
2. **Search before creating** — `search_items` for anything close.
3. **Do the one thing asked**: create, update, link, order, or archive.
4. **Pick the profile** deliberately when creating — it decides how much
   evidence the ticket will owe, and it is the field most often got wrong.
5. **Hand off, or stop.** Filing a ticket is not starting it: if that was the
   whole request, say what you filed and stop there.

The ticket enters at **Backlog**, and this skill leaves it there. Everything
after Backlog belongs to a phase skill — the router at the end says which.

## Orient first: `get_status`, once per session

It tells you whether `.kanmer/` exists, the format version, whether the board
is real or the synthesized default, per-stage counts and any file warnings —
before you write anything. Then `list_board` for the stage/area ids
(they vary per project; writes with unknown ids are rejected, and the error
lists the valid ones) and `list_items` for current state. `search_items`
before creating: if something close already exists, update or link it rather
than filing a near-duplicate.

## Creating tickets

One ticket per unit of work, created before the work starts (`create_item`,
body from `assets/ticket-template.md`). Set `area` from the board's list —
the area determines the ticket's id prefix (a ticket born in `api` becomes
`API-00X`). New tickets belong in the board's first stage (leave `status`
unset). For several at once use `create_items` and check its per-entry results.

**Link or create a governing doc.** The standard board gates *leaving Backlog*
on a governing PRD/FRD/ADR: give the ticket `refs` to the doc it implements
(`link_doc <id> docs/frd/<slug>.md`), or set **`docs_todo: true`** when the doc
is still to be written (hand off to `kanmer-docs`). Without one of the two a
ticket can't leave Backlog. Quick-filed tickets default to `docs_todo`.

Filing a ticket isn't the same as starting it: if the user only asked you to
file one, create it and stop there.

## Epic context

When creating an epic or cross-ticket feature group that needs one shared
approval/constraint contract, write `assets/group-context.md` with
`set_group_doc(path: "context.md")` after creation and read it before members
start. Horizons do not require context by default; membership remains on tickets
through `update_item(groups: [...])`, not a parent/child model.

## Linking

Link once, in one direction. Backlinks are derived, so linking A→B is
enough; `get_links` shows both sides plus typed dependency edges. Use
`link_items rel: "blocks"` when one ticket must land before another — the
blocked ticket shows it automatically. `[[ID]]` inside prose works for
inline references.

## Archive, don't delete

`update_item` with `archived: true` hides an item but keeps it recoverable.
`delete_item` removes the ticket's whole folder — documents and all —
permanently; reserve it for items the user explicitly wants gone.

## Conventions that keep the board useful

- Titles are imperative and specific: "Wire retry logic into upload queue",
  not "Fix bug". The board is read at a glance, so the title carries meaning.
- Ticket bodies say *why* and *how to verify*; the pipeline documents carry
  the depth. Don't duplicate plan.md into the body. The body's **Outcome**
  section stays empty until closeout — in-flight notes go in checklist.md's
  Progress notes.
- When rewriting a ticket body or a document late, pass `expected_updated`
  (the `updated` you last read) so a concurrent edit surfaces as a conflict
  instead of being overwritten.
- Set `due` (YYYY-MM-DD) only when the user gives a real deadline. Priority,
  roughly: `urgent` blocks someone now, `high` is this week, `medium` normal,
  `low` nice-to-have. Ambiguous wording → ask.
- Use `move_item position` ("top" / "bottom" / after another id) when the
  user asks for ordering — "top of the todo column" is meaningful now.
- Move tickets through stages by what the stages *mean* (designing,
  implementing, awaiting eyes, verifying, finished), resolved against
  `list_board` — never hardcoded ids. A move crosses **at most one gated
  boundary**, so walk the stages one at a time; a jump is refused even when
  every document exists, and `update_item status` runs the same check. Gates
  constrain those two calls and nothing else — *creating* a ticket in any stage,
  including the last, is ungated, which is what makes historical backfill
  possible.
- Board changes are shared state: `add_column`/`update_column`/
  `reorder_columns` change what everyone sees, and `remove_column` needs
  `migrate_to` when items still use the column. Ask before restructuring.

## Which skill drives the work

A ticket's life is driven phase by phase; hand off rather than improvising:

    kanmer-tickets → -research → -plan → -execute → -review → -verify → -closeout

That is the order. How far a given ticket actually walks it depends on its
profile, and some profiles stop well short of the end — so ask `get_doc_gates`
rather than assuming every ticket takes every step. Its `reachable` list is the
per-ticket answer.

| You are about to… | Use |
|---|---|
| Investigate a ticket, write its research / files documents | `kanmer-research` |
| Write the plan and checklist | `kanmer-plan` |
| Implement — worktree, branch, checklist, report, PR | `kanmer-execute` |
| Review finished work or handle PR feedback | `kanmer-review` |
| Verify a merged ticket on main → proof | `kanmer-verify` |
| Clean up after the PR merged | `kanmer-closeout` |
| Drive a whole area or group autonomously | `kanmer-auto` |
| Report current state / history | `kanmer-report` |
| Link/create a governing PRD/FRD/ADR | `kanmer-docs` |
| Tidy the backlog itself | `kanmer-groom` |
| Set up or reconcile Kanmer in the repo | `kanmer-setup` |

For exact tool parameters and what each field means, read
`references/tool-reference.md`.

---

**Hand off to `kanmer-research`** when the ticket you just filed is one you were
also asked to work — it is the first phase skill, and it takes the ticket out of
Backlog. If the request was only to manage the board, control returns to the
user here: filing is not starting.
