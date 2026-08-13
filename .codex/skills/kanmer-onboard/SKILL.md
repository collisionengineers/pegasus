---
name: kanmer-onboard
description: Set up Kanmer in a project for the first time — tailor the board's stages and areas to the codebase, and seed the backlog from existing TODOs, roadmaps or the user's head. Use when the user says "set up kanmer", "onboard this project", "create a board for this repo", "import my todos into kanmer", or asks to start tracking work in a project that has no .kanmer folder yet.
---

# Onboarding a project onto Kanmer

Goal: leave the project with a board whose columns match how this team actually
works, and a seeded backlog worth looking at — not an empty default board the
user has to configure themselves.

## Steps

1. **Check state.** Call `list_board`, then `list_items`. The server creates
   `.kanmer/` on first contact, so `list_board` succeeding tells you nothing
   about whether the project has been used before — `list_items` does. If it
   returns items, this isn't a fresh onboard: switch to the normal workflow
   rather than seeding on top of real work.
2. **Learn the project.** Read before proposing structure: README, docs, any
   TODO/ROADMAP/BACKLOG files, issue templates, and the top-level folder names
   (those usually reveal the natural areas — `api/`, `ui/`, `infra/`).
3. **Propose, then apply.** Put it to the user in one short message:
   - **Areas** with hex colours, from the codebase's real seams — 3–6 of them.
     Areas exist so a human can group cards at a glance, and too many defeats
     that. Roadmap *themes* usually make better plans than areas.
   - **Stages** only if the defaults (todo → planning → implementing → review →
     verifying → done) genuinely don't fit. Most projects should keep them,
     since the tools and the GUI assume their meaning.
   Apply the agreed set with one `add_column` call per column
   (`kind: "area"` / `"status"`).
4. **Seed the backlog — tickets first, then plans.** Create the tickets before
   the plans, so each plan's body can reference real ticket ids instead of
   needing a rewrite afterwards. There's no bulk tool, so that's one
   `create_item` per item:
   - TODO/FIXME comments worth tracking → tickets. Put the source location in
     the body as `path/to/file.js:12` so the work stays findable.
   - Roadmap or plan documents → one plan each, with the tickets it covers.
   - Open questions → research notes.
   Use the templates in the `kanmer-workflow` skill's `assets/` folder.
5. **Hand over.** Summarise what you created (N tickets, M plans, the areas),
   mention that the Kanmer desktop app can open this folder to watch the board
   live, and ask what to prioritise first — then set those priorities with
   `update_item`, using priority ids from the `list_board` call in step 1.

## Judgement calls

- Seed selectively: 10 good tickets beat 60 noise tickets. Skip trivial or
  stale TODOs, and tell the user which ones you skipped so they can disagree.
- Never delete or rewrite the user's existing TODO or roadmap files. Kanmer
  items point at them; they don't replace them.
- If the project already has an issue-tracker convention (e.g. GitHub issue
  numbers), carry those references into ticket bodies so the work stays
  traceable both ways.
- `.kanmer/` is plain files, so it can be committed. If the project looks like
  it has multiple contributors, mention that choice rather than deciding it.
