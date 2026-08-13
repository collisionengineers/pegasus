---
name: kanmer-setup
description: Set up or upgrade Kanmer in a project — propose areas and stages, seed a starter backlog, migrate old boards, and install the Kanmer operating instructions into AGENTS.md. Use when the user asks to "set up kanmer", "onboard this project", "start using the board here", "upgrade kanmer", or when a project has no .kanmer folder yet and the user wants work tracked.
---

# Setting up Kanmer in a project

One skill, three modes — detect which one applies before touching anything.

## Detect the mode

Call `get_status` first (it never creates `.kanmer/`):

- **greenfield** — `exists: false`, and `list_items include_archived: true`
  returns nothing: a fresh project. Propose structure, seed a backlog.
- **brownfield** — `exists: false` but the repo has real code/history: mine
  the project for structure and a starter backlog, then proceed like
  greenfield with better-informed proposals.
- **upgrade** — `exists: true` with `format: 1`: the board predates the
  current layout. Run the migration; don't seed anything.

If `exists: true`, `format: 2` and items exist, this project is already set
up — switch to the `kanmer-workflow` skill instead of seeding on top of real
history. An archived-only board is a finished project, not a fresh one.

## Greenfield

1. **Learn the project.** README, docs, TODO/ROADMAP files, top-level folder
   names — those reveal the natural areas (`api/`, `ui/`, `infra/`).
2. **Propose, then apply.** One short message to the user:
   - **Areas** with hex colours and 2–6 letter id prefixes, from the
     codebase's real seams — 3–6 of them. Tickets born in an area carry its
     prefix (`API-001`), so pick prefixes the user will like reading.
     **PR Review** (prefix `PR`) is already on every new board — keep it.
   - **Stages** only if the defaults (todo → planning → implementing →
     review → verifying → done) genuinely don't fit. You can now apply stage
     changes yourself: `add_column` / `update_column` / `reorder_columns`,
     and `remove_column` (with `migrate_to` if items already sit in one).
     Still bias toward keeping the defaults — the proof gate lives on the
     LAST stage and new items land in the FIRST, so a restructure changes
     behaviour, not just labels.
3. **Apply areas** with one `add_column` call each (`kind: "area"`, with
   `color` and `prefix`).
4. **Seed the backlog** with a single `create_items` call (tickets only —
   plans and research live inside tickets as documents now). Check the
   per-entry results. Body from the workflow skill's ticket template; leave
   `status` unset so everything lands in the first stage.
5. **Install the operating instructions** (below), then finish with
   `get_status` and report what was created.

## Brownfield

Same as greenfield, plus: mine the code for the starter backlog — TODO/FIXME
comments, failing or skipped tests, README "known issues", half-finished
directories. Propose the backlog to the user before bulk-creating; their
repo, their priorities. Link related tickets as you create them
(`links` at create time, `rel: "blocks"` where order genuinely matters).

## Upgrade

1. `get_status` confirms `format: 1`.
2. Tell the user what migration does: tickets move into
   `areas/<area>/<id>/` folders, legacy plans/research fold into the tickets
   they relate to (as plan.md / research.md) or become tickets labelled
   `legacy-plan`/`legacy-research` if nothing links them, areas get pinned id
   prefixes, ids never change.
3. The migration itself runs from the Kanmer app (it prompts on opening a v1
   board) — ask the user to click **Migrate to v2** there. If they'd rather
   you drive, they can run it via the GUI once and you verify after.
4. Verify with `get_status` (`format: 2`, counts intact) and summarize what
   moved. Then refresh the AGENTS.md block (below) — upgrade mode only ever
   rewrites between the markers.

## The AGENTS.md operating instructions (all three modes)

Every mode ends by making sure the target repo's `AGENTS.md` **begins** with
this managed block — it's how any agent that opens the repo learns the board
exists and how to behave on it:

```markdown
<!-- kanmer:instructions:start — managed by kanmer-setup; edits inside will be overwritten -->
# Kanmer operating instructions

This repo's work is tracked on a Kanmer board in `.kanmer/`.

- Start every session with `get_status`, then `list_board` / `list_items` to find your ticket.
- Take a ticket before working: `take_ticket` records the time, branch and worktree, and moves the stage.
- Follow the doc pipeline in the ticket's folder: research.md + impact.md → plan.md → checklist.md → proof.md.
- proof.md is required before a ticket can reach the final stage.
- Add progress notes with `set_ticket_doc` (append: true) — don't rewrite whole documents to add a line.
- Archive, don't delete. Reference other items with [[ID]] wiki-links.
<!-- kanmer:instructions:end -->
```

Rules:

- The block is the **very first thing in the file** — above any existing
  content, so it's read before anything else.
- `AGENTS.md` missing → create it with the block plus a stub heading for the
  repo's own content (e.g. `# <project> contributor guide`).
- `AGENTS.md` present → insert the block at the top; **never modify anything
  outside the markers**.
- Block already present → refresh only the content **between the markers**
  (idempotent — running setup twice changes nothing the second time).
- If a `CLAUDE.md` exists and doesn't reference `AGENTS.md`, add a one-line
  pointer to it.
