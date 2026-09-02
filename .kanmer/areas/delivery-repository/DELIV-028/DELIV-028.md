---
id: DELIV-028
type: ticket
title: Restore the design authority after the design-system removal
status: done
area: delivery-repository
order: 2270
assignee: claude-fable-5
profile: chore
stageEntered:
  implementing: '2026-08-27T08:50:04.573Z'
  review: '2026-08-27T08:50:15.945Z'
  verifying: '2026-08-27T08:51:45.070Z'
  done: '2026-08-27T09:23:40.935Z'
labels:
  - documentation
  - design
links:
  - UIIMP-004
  - DELIV-027
refs:
  - docs/index.md
commits:
  - 0925d990
prs:
  - '569'
deployment: n/a
archived: false
created: '2026-08-27T08:49:00.457Z'
updated: '2026-09-01T14:44:33.869Z'
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
