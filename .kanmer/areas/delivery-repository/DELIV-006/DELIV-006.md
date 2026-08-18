---
id: DELIV-006
type: ticket
title: Capture the Claude Design github.md screen map in the repository
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - design
  - documentation
links: []
docs_todo: true
archived: false
created: '2026-08-18T09:39:12.336Z'
updated: '2026-08-18T09:39:12.336Z'
---

## What

Copy or record the Claude Design project's `github.md` screen map — which maps every prototype to the Razor page it was built from — as a repository reference artefact.

## Why

The Claude Design project `710bb42f` carries a `github.md` that maps every screen prototype to the Razor page it was built from. [[PLAT-001]]'s research used it as the authoritative file mapping, and it proved genuinely useful — but it currently lives only inside the Claude Design project, not in the repository. If the design project is lost or re-synced, the mapping is gone.

## Approach

- Copy `github.md` from the Claude Design project to `docs/design/screen-map.md` (or `docs/design/claude-design-screen-map.md`).
- Update `docs/index.md` to link it.
- Record it as a reference artefact, not a governing doc.

## Verification

- [ ] The screen map exists in the repository under `docs/design/`.
- [ ] `docs/index.md` links to it.

## Outcome
