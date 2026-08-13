# Impact — Rewrite AGENTS.md

## Files changed
- `AGENTS.md` only — rewrite "Planning process" + "Repository task workflow"
  so the claimable unit is a Kanmer ticket (`take_ticket`) instead of a
  `NOW.md` claim line; remove the "bump the NOW.md date" rule. `CLAUDE.md`
  updates automatically via the symlink (no separate edit).

## Explicitly preserved
- The `kanmer:instructions` managed block (lines 1–12).
- "Safety rails" and "Product invariants" sections (still current).
- Filename `AGENTS.md`, the symlink, and the `#repository-task-workflow`
  anchor.

## Coordination
- The workflow rewrite IS [[SIMPLI-004]]'s coordination point → both land in
  one Stage-B edit to avoid a broken intermediate.
- [[SIMPLI-006]] makes `docs/index.md` self-contained and keeps workflow
  ownership in AGENTS.md — do 006 first so the index target is stable.
- No code/test changes (the 7 tests only need the filename to persist).
