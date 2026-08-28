# Plan — KANMER-006 reconcile the current Kanmer setup drift

## Diff estimate

- `AGENTS.md`: managed block body replaced with the literal body shipped in
  Kanmer 0.3.3 `kanmer-setup/SKILL.md` (~12 lines changed); the board-branch
  paragraphs and the "Agent conduct" section that currently sit *inside* the
  markers move verbatim to just below the end marker so nothing is lost.
- `.grok/skills/**`: 15 tracked files overwritten with the bundled 0.3.3
  copies (the `get_status` "differ" list). Extra tracked assets not shipped by
  0.3.3 stay; they are not flagged.
- No code, no board.yml, no `.worktrees/` change. Roughly 40 files, mostly
  wholesale template refreshes.

## Steps

1. Reconcile `AGENTS.md`: `get_status` compares the between-marker body by
   SHA-256 against the bundled skill's block, so the body must match exactly.
   Rewrite the body from the bundle; relocate the repo-authored text that had
   been placed inside the markers to directly after `<!-- kanmer:instructions:end -->`.
   The `scripts/agents-block.mjs` script is not in the plugin install, so this
   is the hand-edit path the skill allows.
2. Reconcile `.grok/skills`: copy every file under the bundled
   `plugins/kanmer/skills` tree over the tracked copy (idempotent copy).
3. `.claude/skills` is gitignored (`/.claude/` in `.gitignore`) and only
   exists in the main checkout, so it cannot be reconciled through a PR; the
   fix is a local copy of the bundled `kanmer-setup/SKILL.md` plus the stamp,
   done by the operator (or "reconnect" in the Kanmer app). Record in scratch.
4. Board hygiene: `update_item TICK-222 area: delivery-repository`; note the
   `C:/Users/Alex/…` worktree paths on CASE-024 and MAIL-017 in scratch.
5. Commit in slices (AGENTS.md; .grok/skills), push, PR to `dev`.

## Reuse

Bundled skills tree and SKILL.md block body are the single source; no new
files, no scripts added.

## Simplification pass

n/a — docs-only.
