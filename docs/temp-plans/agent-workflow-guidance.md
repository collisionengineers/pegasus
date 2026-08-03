# Task plan: agent-workflow-guidance

Bootstrap note: this task predates the protocol it introduces, so it was not
claimed by a push to `dev`; its PR merges under the previous
`MERGE AUTH GRANTED` rule.

## Goal

Introduce the multi-agent task workflow (operator-decided 2026-08-03) and
correct guidance that no longer matches reality.

## Changes

- ADR-0017 records the decisions; `docs/adr/README.md` indexes it.
- `docs/temp-plans/` created with its contract README.
- `NOW.md` restructured: uncapped `Doing` (live claims) and `Next` (ordered
  queue), rewritten rules footer with the staleness ladder.
- `AGENTS.md`: planning process describes take → worktree → plan → PR →
  independent review → release; git rail narrowed to "preserve work that is
  not yours" with explicit allowances; `MERGE AUTH GRANTED` scoped to
  `dev` → `main`.
- `docs/engineering.md`: corrected branch model (task → `dev` → `main` =
  deployment), full task-workflow protocol, softened commit-subject rule,
  Markdown convention.
- `docs/index.md`: temp-plans carve-out, NOW.md row rewording,
  engineering.md and design/README.md ranked in the authority chain.
- `docs/operations.md`: work-tracking paragraph updated.
- `.github/workflows/ci.yml`: `changes` job (Linux path detector, no
  application evidence) gates the build/test steps and `qdos-pressure` on a
  build-relevant allowlist — `src/`, `tests/`, `Pegasus.slnx`,
  project/props/targets/lock/config files, CI-executed scripts; everything
  else runs only the documentation link check.
- Review fixes (Codex + operator-approved): claim retry and abandonment
  reset to `origin/dev` instead of re-pushing a stale tip; three-job CI
  description in engineering.md/operations.md/ADR-0017.
- `scripts/Test-DocumentationLinks.ps1`: excludes `docs/temp-plans/` (except
  its README).
- `.gitignore`: ignores `/.claude/`.
- Formatting: blank lines inserted before headings in `docs/requirements.md`,
  `docs/operations.md`, `design/README.md`, `design/product/ui-spec.md`.

Deliberately excluded: the traceability-matrix consolidation is its own
follow-up task (`task/consolidate-traceability-matrix`); the vendored
`.agents/skills` stay unbound from the tracker.

## Verification

- `scripts/Test-DocumentationLinks.ps1` passes locally.
- CI on this PR runs the full build (it touches non-Markdown files).
- A follow-up Markdown-only PR proves the docs-only path-skip.
