# agents-omp-trim

Trim the vendored `.agents/skills/` library to the four operator-kept
skills and delete the unused `.omp/` harness directory (operator
decisions 2026-08-05/06; restructure wave 1).

## What changes

- `.agents/skills/`: delete sixteen packages; keep exactly `grill-me`,
  `grill-with-docs`, `grilling` (the engine both wrappers invoke), and
  `domain-modeling` (a grill-with-docs dependency that also prescribes
  the CONTEXT.md format).
- `skills-lock.json`: prune to the four kept entries; hashes unchanged
  (kept files are not modified).
- `.omp/`: delete the whole directory (11 agent definitions) — the
  operator uses Codex and Claude only.
- `NOW.md`: this PR removes its own claim line.

Deliberately untouched: `.codex/` (in use), `AGENTS.md`/`CLAUDE.md` (the
symlink pair is load-bearing), ADR-0009's `.agents/skills/` statement
(stays true — the directory survives with four skills), and the protected
`workspaces/ai-centre/skills/` packages (unrelated tree; never touched).

## How verified

Reference sweep before deletion found no inbound links that break: the CI
documentation check excludes `.agents/` and `.codex/`; nothing outside
`.omp/` references `.omp/`; ADR-0009 references the directory, not the
removed packages. After deletion: `.agents/skills/` lists exactly the
four kept packages plus their files, `skills-lock.json` parses and lists
exactly four entries, and no tracked file outside the deleted trees
changes except `skills-lock.json` and `NOW.md`. CI: build lanes
path-skip (no build-relevant path); the documentation job must pass.
