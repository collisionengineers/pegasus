---
name: kanmer-workflow
description: Track and organise work in Kanmer, the file-based kanban shared live with a human's board GUI. Use this whenever the user asks to plan work, track tasks, create or update tickets, manage a backlog, record research, break a feature into steps, or asks "what's on the board" / "add a ticket for this" — and also proactively when you start a multi-step piece of work in a project that contains a .kanmer folder, so the human can follow your progress on their board.
---

# Working with Kanmer

Kanmer stores tickets as folders in the project's `.kanmer/` tree — each
ticket owns its markdown file plus a pipeline of documents (research, impact,
plan, checklist, proof) that live beside it. The human sees the same data on a
live kanban board (the Kanmer desktop app), so every item you create or move
is instantly visible to them — treat the board as your shared workspace, not
a log.

All access goes through the `kanmer` MCP tools. Never edit `.kanmer/` files
directly; the tools keep ids, timestamps, folders and frontmatter consistent.

## The ticket lifecycle

1. **Orient first: `get_status`, once per session.** It tells you whether
   `.kanmer/` exists, the format version, whether the board is real or the
   synthesized default, per-stage counts and any file warnings — before you
   write anything. Then `list_board` for the stage/area/priority ids (they
   vary per project; writes with unknown ids are rejected, and the error
   lists the valid ones) and `list_items` for current state. `search_items`
   before creating: if something close already exists, update or link it
   rather than filing a near-duplicate.
2. **One ticket per unit of work**, created before you start (`create_item`,
   body from `assets/ticket-template.md`). Set `area` from the board's list —
   the area determines the ticket's id prefix (a ticket born in `api` becomes
   `API-00X`) — and put PR feedback in the default **PR Review** area. New
   tickets belong in the board's first stage (leave `status` unset). Filing a
   ticket isn't the same as starting it: if the user only asked you to file
   one, create it and stop there. For several at once use `create_items` and
   check its per-entry results.
3. **Take the ticket before working it.** `take_ticket` with the real
   `branch` (and `worktree` if you're in one) records when and where the work
   is happening and moves the ticket to the working stage. The human's board
   shows the ⛏ taken badge — that's how they know it's live. If it's already
   taken, coordinate rather than passing `force`.
4. **Work the document pipeline** with `get_ticket_doc` / `set_ticket_doc`:
   - **research.md** — what you learned (template: `assets/research-template.md`);
   - **impact.md** — the files/modules the change touches and how
     (`assets/impact-template.md`);
   - **plan.md** — written FROM research + impact, never before them
     (`assets/plan-template.md`);
   - **checklist.md** — the plan distilled into `- [ ]` steps
     (`assets/checklist-template.md`); tick items as you complete them and
     add progress notes with `set_ticket_doc(append: true)` — never resend a
     whole document just to add a line;
   - **proof.md** — the evidence it works: test output, commands run, what
     you observed (`assets/proof-template.md`). **Required**: the board
     rejects moving a ticket to the final stage without it.
5. **Move through the stages as you work**, choosing ids from `list_board` by
   what they *mean* — designing, implementing, awaiting the user's eyes,
   verifying, finished. On a fresh board that's `planning` → `implementing` →
   `review` → `verifying` → `done`; older boards commonly differ. Move as you
   go rather than batching at the end, and don't mark something done you
   haven't checked — proof.md first, then `move_item` to the final stage.
6. **Release when you stop.** `take_ticket action: "release"` clears
   taken/branch/worktree — whether the work is finished or you're handing it
   back. A ticket left taken looks in-progress to everyone.
7. **Link once, in one direction.** Backlinks are derived, so linking A→B is
   enough; `get_links` shows both sides plus typed dependency edges. Use
   `link_items rel: "blocks"` when one ticket must land before another — the
   blocked ticket shows it automatically. `[[ID]]` inside prose works for
   inline references.
8. **Archive, don't delete.** `update_item` with `archived: true` hides an
   item but keeps it recoverable. `delete_item` removes the ticket's whole
   folder — documents and all — permanently; reserve it for items the user
   explicitly wants gone.

## Conventions that keep the board useful

- Titles are imperative and specific: "Wire retry logic into upload queue",
  not "Fix bug". The board is read at a glance, so the title carries meaning.
- Ticket bodies say *why* and *how to verify*; the pipeline documents carry
  the depth. Don't duplicate plan.md into the body.
- When rewriting a ticket body or a document late, pass `expected_updated`
  (the `updated` you last read) so a concurrent edit surfaces as a conflict
  instead of being overwritten.
- Set `due` (YYYY-MM-DD) only when the user gives a real deadline. Priority,
  roughly: `urgent` blocks someone now, `high` is this week, `medium` normal,
  `low` nice-to-have. Ambiguous wording → ask.
- Use `move_item position` ("top" / "bottom" / after another id) when the
  user asks for ordering — "top of the todo column" is meaningful now.
- Board changes are shared state: `add_column`/`update_column`/
  `reorder_columns` change what everyone sees, and `remove_column` needs
  `migrate_to` when items still use the column. Ask before restructuring.

For exact tool parameters and what each field means, read
`references/tool-reference.md`.
