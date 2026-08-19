---
id: PLAT-001
type: ticket
title: Claude Design UI implementation
status: done
area: platform-operations
order: 310
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-17T12:57:54.027Z'
  review: '2026-08-17T14:33:41.674Z'
  verifying: '2026-08-18T09:23:15.420Z'
  done: '2026-08-18T09:36:55.367Z'
labels:
  - ui
  - design
links:
  - ENG-001
  - CASE-002
  - CASE-004
  - PLAT-008
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - 196e65ac
  - 23b88b8d
  - f9cd4b9a
  - b5bf39a5
  - 7206773a
  - 9988c1d8
  - d4bb25ad
  - 97ea8b4d
  - 269deec1
  - 8c3ef48a
  - 8b3a784b
  - ac346686
  - fe44ec8a
prs:
  - '397'
archived: false
created: '2026-08-17T12:29:59.429Z'
updated: '2026-08-19T11:01:45.376Z'
---

## What

Take a UI produced in Claude Design and implement it for this project — translating the supplied design into the Pegasus operator interface.

## Why

The UI direction now exists as a Claude Design output rather than as working screens. This ticket carries that design across into the application so operators use it, rather than leaving it as an external artefact.

## Approach

- Capture the Claude Design source (screens, tokens, components) as ticket reference material before any code changes.
- Reconcile it against `docs/design/README.md`, which is the binding UI authority — where the two disagree, the repository design rules win and the difference is recorded.
- Map each design screen to the operator journeys required by FRD-12 before building.
- Implement in Pegasus.Web only; no business policy moves out of `Pegasus.Core`.

## Verification

- [x] `dotnet build --configuration Release` and the focused test profile pass.
- [x] Implemented screens match the design and the states FRD-12 requires (loading, empty, stale, partial, failed, validation, conflict, access-denied).
- [ ] Visual proof captured from a local run — browser suite (32 tests) covers the same routes through Playwright; screenshot capture remains a follow-up.

## Outcome

Shipped to `dev` via PR #397 (merged 2026-08-18). All 21 design screens implemented in `Pegasus.Web` with the left rail shell, 10 commissioned marks, and deferred-capability markup. All test suites green on merged dev (580 Core, 96 Architecture, 504 integration, 32 browser).

Follow-up tickets worth filing:
1. Rail counts: decide the query and wire real outstanding figures.
2. Experian AutoCheck has no capability ID — needs inventory entry, supplier contract, ADR.
3. Case notes and engineer queries — shown in the prototype, unallocated.
4. The design project's `github.md` screen map is a useful artefact, currently only in the Claude Design project.
5. Four unplaced marks (`activity`, `brand`, `calendar`, `casefolder`) need surfaces or a decision to retire them.
6. Visual screenshots from a local `DevelopmentOffline` run.
