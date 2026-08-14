---
name: kanmer-setup
description: Set up or upgrade Kanmer in a project — propose areas and stages, seed a starter backlog, migrate old boards, and install the Kanmer operating instructions into AGENTS.md. Use when the user asks to "set up kanmer", "onboard this project", "start using the board here", "upgrade kanmer", or when a project has no .kanmer folder yet and the user wants work tracked.
---

# Setting up Kanmer in a project

One skill, three modes — detect which one applies before touching anything.

## Detect the mode

Call `get_status` first (it never creates `.kanmer/`). When setup is performed
through the GUI in a Git repository, its canonical board worktree is
`.worktrees/kanmer` on the configured `kanmer-board` branch; MCP is already
rooted there. Do not create, switch, or push that board branch from an agent.

- **greenfield** — `exists: false`, and `list_items include_archived: true`
  returns nothing: a fresh project. Propose structure, seed a backlog.
- **brownfield** — `exists: false` but the repo has real code/history: mine
  the project for structure and a starter backlog, then proceed like
  greenfield with better-informed proposals.
- **upgrade** — `exists: true` with `format: 1`: the board predates the
  current layout. Run the migration; don't seed anything.

If `exists: true`, `format: 2` and items exist, this project is already set
up — switch to the `kanmer-tickets` skill instead of seeding on top of real
history. An archived-only board is a finished project, not a fresh one.

## Greenfield (brief → docs → board)

A real product brief can imply a hundred tickets — so this flow is
**docs-first**, and nothing is created until the user confirms a preview.

0. **Require a brief.** If the user hasn't given one, ask for a paragraph or two:
   what they're building and for whom. Don't invent a product; a fresh repo with
   no brief means stop and ask.
1. **Annotate the brief → `docs/product/vision.md`** — the durable statement of
   what and why.
2. **Split into governing docs** via `kanmer-docs`: each product span becomes a
   **PRD** (`docs/prd/`), each PRD's behaviour a **FRD** (`docs/frd/`, with
   acceptance criteria), each cross-cutting decision an **ADR** (`docs/adr/`).
   Unresolved questions go to `docs/product/open-questions.md` — surfaced, not
   guessed.
3. **Materialise the `/docs/` tree** + `docs/contributing/doc-structure.md` (from
   `kanmer-docs`'s `doc-structure` template).
4. **Board setup — preview first.** Propose, in one message the user confirms:
   - **Areas** (3–6) from the FRDs/ADRs, plus the 4 built-in areas (Bugs, PR
     Review, UI, Documentation) with their default doc-sets; hex colours + 2–6
     letter prefixes.
   - **Stages** only if the 7 defaults (backlog → researching → planning →
     implementing → review → verifying → done) don't fit — bias to keeping them
     (the gates and stage contract assume them).
   - **The backlog with counts** — "N PRDs → M FRDs → K tickets" — because a real
     brief can yield 100+ tickets. Only after the user confirms: `add_column`
     each area, then one `create_items` call — **one ticket per FRD acceptance-
     criterion / ADR consequence**, each created with **`refs`** to the doc it
     implements (so it satisfies the leave-Backlog gate; use `docs_todo` only
     where a doc is still owed). Leave `status` unset (first stage).
5. **Wire it up**: ensure `.worktrees/` is in `.gitignore`; install the operating
   instructions (below); ask whether to keep the **PRD/FRD/ADR gate on** (the
   default) or disable it for a repo that declines a `/docs/` tree. Finish with
   `get_status` + report the doc → ticket link map.

## Brownfield

Same as greenfield, plus: mine the code for the starter backlog — TODO/FIXME
comments, failing or skipped tests, README "known issues", half-finished
directories. Propose the backlog to the user before bulk-creating; their
repo, their priorities. Link related tickets as you create them
(`links` at create time, `rel: "blocks"` where order genuinely matters).

If the repo tracks work in GitHub issues, don't re-derive those from the
code — that's the `kanmer-import` skill's job (it's idempotent and records
source URLs); run it as part of seeding instead of duplicating its logic.

## Upgrade

1. `get_status` confirms `format: 1`.
2. Tell the user what migration does: tickets move into
   `areas/<area>/<id>/` folders, legacy plans/research fold into the tickets
   they relate to (as plan.md / research.md) or become tickets labelled
   `legacy-plan`/`legacy-research` if nothing links them, areas get pinned id
   prefixes, ids never change.
3. The migration runs from the Kanmer app (it prompts on opening a v1 board) —
   ask the user to click **Migrate to v2**. As an agent you can also drive it
   with the **`migrate_board`** tool (`dry_run: true` first to preview — it also
   backfills the 7-stage default). Either way it's additive and idempotent.
4. **Don't let the new gates strand old tickets.** Set `docs_todo: true` on
   existing tickets that have no governing-doc `refs` yet, so the leave-Backlog
   gate doesn't retroactively block work already in flight. `kanmer-groom`
   surfaces this doc-gate debt later. (Or, per §Greenfield step 5, the user can
   disable the PRD/FRD/ADR gate entirely for a repo that declines `/docs/`.)
5. Verify with `get_status` (`format: 2`, counts intact) and summarize what
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

- Start every session with `get_status`, then `list_board` / `list_items` to find your ticket. `get_doc_gates` shows which documents each stage transition needs.
- Work each ticket on its own branch and worktree: worktree `.worktrees/<id>`, branch `<id>-<slug>`; `take_ticket` records both and moves the stage.
- Stages: backlog → researching → planning → implementing → review → verifying → done — hard document gates guard the transitions.
- Before a ticket leaves Backlog, link a governing doc (`link_doc` → a PRD/FRD/ADR in `/docs/`) or set `docs_todo`.
- Doc pipeline: research.md + impact.md → plan.md → checklist.md → post-implementation-report.md; write proof.md on merged main before Done.
- Add running notes with `append_scratch` (not `set_ticket_doc`) — scratch is the notepad and is never gated.
- Review passes → the PR is merged → the ticket enters Verifying; write proof.md on merged main, move to Done, then close out (record commits/PRs/deployment).
- Archive, don't delete. Reference other items with [[ID]] wiki-links.
- Skills, one per phase: kanmer-tickets (manage), -docs, -research, -plan, -execute, -review, -verify, -closeout, -auto, -report, -groom, -import, -setup.
<!-- kanmer:instructions:end -->
```

**Don't hand-edit — run the script that owns this block:**

```
node <plugin-root>/../../scripts/agents-block.mjs <repo> ["# <project> contributor guide"]
```

It implements all four rules below, is idempotent, and refuses to guess at a
file whose markers are malformed. `scripts/verify-agents-block.mjs` in the
Kanmer repo is its end-to-end test. The block text above is the literal body
the script writes — keep the two in step if you change either.

Hand-edit **only** if the script isn't available (a plugin install without the
repo checked out), in which case the rules are:

- The block is the **very first thing in the file** — above any existing
  content, so it's read before anything else.
- `AGENTS.md` missing → create it with the block plus a stub heading for the
  repo's own content (e.g. `# <project> contributor guide`).
- `AGENTS.md` present → insert the block at the top; **never modify anything
  outside the markers**.
- Block already present → refresh only the content **between the markers**
  (idempotent — running setup twice changes nothing the second time). Leave the
  block where it already sits; moving it would rewrite bytes outside the
  markers.
- If a `CLAUDE.md` exists and doesn't reference `AGENTS.md`, add a one-line
  pointer to it.
- If the markers are malformed (end before start, or only one present), stop
  and tell the user — never guess at a half-marked file.
