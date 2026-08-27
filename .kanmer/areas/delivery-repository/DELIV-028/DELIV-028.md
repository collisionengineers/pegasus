---
id: DELIV-028
type: ticket
title: Restore the design authority after the design-system removal
status: preparing
area: delivery-repository
assignee: claude-fable-5
profile: chore
taken_at: '2026-08-27T08:49:13.116Z'
branch: task/docs-007-restore-design-readme
worktree: ../pegasus-worktrees/docs-007-restore-design-readme
labels:
  - documentation
  - design
links:
  - UIIMP-004
  - DELIV-027
refs:
  - docs/index.md
deployment: n/a
archived: false
created: '2026-08-27T08:49:00.457Z'
updated: '2026-08-27T08:49:13.116Z'
---

## Why

Commit `9eec6dc2` ("design docs removed", merged to `main` via #568) deleted
`docs/design/system/`, `docs/design/references/mockups/`, `.design-sync/`
and `design/planning-and-old-designs/` — and with them
`docs/design/README.md`, the design authority cited by CLAUDE.md,
`docs/index.md`, the FRDs, capabilities, current-architecture and the marks
README. Twelve relative links broke and the `documentation` CI check fails on
every PR; PR #562 conflicts on the file.

Operator decision 2026-08-27: restore the README only; everything else stays
deleted.

## Scope

- Restore `docs/design/README.md` from `a4da02a5`.
- Remove its references to the deleted folders (design-system preview logo
  row, Claude Design system bindings row, the three comparison-raster links).
- `Test-DocumentationLinks.ps1` passes.

## Outcome
