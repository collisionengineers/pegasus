---
id: PLAT-011
type: ticket
title: >-
  Resolve actor display names for the Automation activity and case summary
  surfaces
status: implementing
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-20T04:31:21.173Z'
taken_at: '2026-08-20T03:27:58.836Z'
branch: task/plat-011-actor-display-names
worktree: ../pegasus-worktrees/plat-011
labels:
  - ui
  - design
  - identifiers
links:
  - PLAT-010
refs:
  - docs/design/README.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-19T23:48:17.177Z'
updated: '2026-08-20T04:31:21.173Z'
---

## What

Two operator-facing surfaces still render **raw GUIDs as identity** and cannot be fixed by copy alone:

- `Pages/Administration/Automation/Activity.cshtml` — the "Subject" column shows the actor's raw subject id.
- `Pages/Cases/Shared/_CaseSummary.cshtml` — the "Actor" row shows a raw subject id.

Resolve each to a display name (staff account name; the Automation client's name for automation actors) via the owning query/handler, with an honest fallback when no name exists. The internal-identifiers rule (`docs/design/README.md:168`) is the authority.

## Why

Found and correctly **not** botched by [[PLAT-010]]: no display-name field exists on the underlying records, so fixing it needs a handler/query change — outside that copy-only ticket's scope. Reported there; owned here.

## Verification

- [ ] Both surfaces show names, never GUIDs; unknown actors show an honest label, not an invented one.
- [ ] Query changes live in the existing query owners; Web tests updated; Browser suite green.

## Outcome
