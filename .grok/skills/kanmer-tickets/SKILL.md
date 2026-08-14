---
name: kanmer-tickets
description: Manage tickets on Kanmer, the file-based kanban shared live with a human's board GUI — orient on the board, create, update, link, order and archive tickets, and keep the board's conventions. Use whenever the user asks to add or update a ticket, manage the backlog, or asks "what's on the board" / "add a ticket for this" — and also proactively when you start a multi-step piece of work in a project that contains a .kanmer folder, so the work is filed before it starts and the human can follow it. DO NOT USE FOR doing the work itself — researching a ticket (kanmer-research), planning it (kanmer-plan), implementing it (kanmer-execute), reviewing it (kanmer-review), post-merge cleanup (kanmer-closeout), or clearing a whole area (kanmer-auto).
---

# Managing Kanmer tickets

Kanmer stores tickets as folders in the project's `.kanmer/` tree — each
ticket owns its markdown file plus a pipeline of documents (research, impact,
plan, checklist, proof) that live beside it. The human sees the same data on a
live kanban board (the Kanmer desktop app), so every item you create or move
is instantly visible to them — treat the board as your shared workspace, not
a log.

All access goes through the `kanmer` MCP tools. Never edit `.kanmer/` files
directly; the tools keep ids, timestamps, folders and frontmatter consistent.

This skill covers the tickets themselves. The work *inside* a ticket is
driven by the phase skills — see the router at the end.

## Orient first: `get_status`, once per session

It tells you whether `.kanmer/` exists, the format version, whether the board
is real or the synthesized default, per-stage counts and any file warnings —
before you write anything. Then `list_board` for the stage/area/priority ids
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
  `list_board` — never hardcoded ids. Moving to the **final** stage requires
  the ticket's proof.md to exist.
- Board changes are shared state: `add_column`/`update_column`/
  `reorder_columns` change what everyone sees, and `remove_column` needs
  `migrate_to` when items still use the column. Ask before restructuring.

## Which skill drives the work

A ticket's life is driven phase by phase; hand off rather than improvising:

| You are about to… | Use |
|---|---|
| Investigate a ticket, write research.md / impact.md | `kanmer-research` |
| Write plan.md / checklist.md | `kanmer-plan` |
| Implement — worktree, branch, checklist, proof, PR | `kanmer-execute` |
| Review finished work or handle PR feedback | `kanmer-review` |
| Clean up after the PR merged | `kanmer-closeout` |
| Clear a whole area's tickets autonomously | `kanmer-auto` |
| Report current state / history | `kanmer-report` |
| Link/create a governing PRD/FRD/ADR | `kanmer-docs` |
| Verify a merged ticket → proof.md | `kanmer-verify` |
| Tidy the backlog itself | `kanmer-groom` |
| Pull GitHub issues or PR comments onto the board | `kanmer-import` |

For exact tool parameters and what each field means, read
`references/tool-reference.md`.
