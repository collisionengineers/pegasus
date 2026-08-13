---
name: kanmer-workflow
description: Track and organise work in Kanmer, the file-based kanban shared live with a human's board GUI. Use this whenever the user asks to plan work, track tasks, create or update tickets, manage a backlog, record research, break a feature into steps, or asks "what's on the board" / "add a ticket for this" — and also proactively when you start a multi-step piece of work in a project that contains a .kanmer folder, so the human can follow your progress on their board.
---

# Working with Kanmer

Kanmer stores tickets, plans and research notes as Markdown files in the
project's `.kanmer/` folder. The human sees the same data on a live kanban
board (the Kanmer desktop app), so every item you create or move is instantly
visible to them — treat the board as your shared workspace, not a log.

All access goes through the `kanmer` MCP tools. Never edit `.kanmer/` files
directly; the tools keep ids, timestamps and frontmatter consistent.

## The working loop

1. **Orient first.** Call `list_board` once per session for the stages, areas
   and priorities — ids vary per project, and inventing one silently mis-files
   the item. Default stages: todo → planning → implementing → review →
   verifying → done. Then `list_items` for current state, and `search_items`
   before creating: if something close already exists, update or link it rather
   than filing a near-duplicate.
2. **One ticket per unit of work**, created before you start (`create_item`
   with `type: "ticket"`, body from `assets/ticket-template.md`). New tickets
   belong in `todo` — filing a ticket isn't the same as starting it, so if the
   user only asked you to file one, create it and stop there. Set `area` and
   `priority` from step 1's ids; if the board has no areas defined yet, omit
   `area` rather than inventing one. Labels are free-form — use them only where
   the project already has a convention.
3. **Move through the stages as you work.** Call `move_item` at each real
   transition: `planning` while you design the approach, `implementing` while
   you write code, `review` when it needs the user's eyes, `verifying` while
   tests or checks run, `done` once verified. The human reads these transitions
   to know where you are, so move as you go rather than batching at the end —
   and don't mark something done that you haven't actually checked.
4. **Plans coordinate tickets.** For multi-ticket work, create the tickets
   first and then the plan (`assets/plan-template.md`) with their real ids in
   its table — that order saves you rewriting the plan body afterwards. Give
   each ticket `links: ["PLAN-00X"]` once the plan exists, or link it later with
   `link_items`. Note that `update_item` replaces the whole `body`, so a late
   edit means re-sending it in full.
5. **Research feeds decisions.** Findings worth keeping outlive the
   conversation, so put them in a research note
   (`assets/research-template.md`), linked from the ticket or plan that
   prompted it. An open question with no findings yet isn't a research note —
   it belongs in a plan's Risks section.
6. **Link once, in one direction.** Backlinks are derived, so linking A→B is
   enough; `get_links` shows both sides. Use `links` at create time where you
   can, `link_items` for relations you discover later, and `[[ID]]` inside a
   body for inline references while writing prose.
7. **Archive, don't delete.** `update_item` with `archived: true` hides an item
   from the board but keeps it recoverable. `delete_item` is permanent —
   reserve it for items the user explicitly wants gone.

Plans and research notes carry a status like anything else, but they aren't
worked through the stages the way tickets are. Leave them in `todo` unless the
user wants them tracked on the board.

## Conventions that keep the board useful

- Titles are imperative and specific: "Wire retry logic into upload queue", not
  "Fix bug". The board is read at a glance, so the title carries the meaning.
- Bodies say *why* and *how to verify*, not just *what*. Drop template sections
  that genuinely don't apply; `## Notes` in particular starts empty and fills up
  as you work.
- Priority, roughly: `urgent` is blocking someone right now, `high` is this
  week, `medium` is normal, `low` is nice-to-have. If the user's wording is
  genuinely ambiguous, ask instead of guessing.
- If the board's stages don't fit the work, ask before restructuring —
  `add_column` changes the board for everyone who looks at it.

For exact tool parameters and what each field means, read
`references/tool-reference.md`.
