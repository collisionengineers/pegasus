---
id: CASE-026
type: ticket
title: >-
  Port the Search page (/Search) with the advanced filter grid and selected-Case
  pane
status: done
area: case-reference-workflow
assignee: zcode
profile: feature
stageEntered:
  implementing: '2026-08-28T18:45:18.734Z'
  review: '2026-08-28T19:04:46.136Z'
  verifying: '2026-08-29T09:19:43.975Z'
  done: '2026-08-29T17:24:37.269Z'
taken_at: '2026-08-28T18:33:20.835Z'
branch: task/case-026-search-page
worktree: ../pegasus-worktrees/case-026-search-page
labels:
  - ui
  - wave-2
  - search
groups:
  - EPIC-011
links:
  - PLAT-059
  - UIIMP-011
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - 882f32ae
  - 20843a7e
  - 9d739ab9
  - 17930a17
  - 0f80c363
  - 56ce7898
  - d2ce04fe
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/606'
archived: false
created: '2026-08-28T08:35:23.906Z'
updated: '2026-08-29T17:24:37.269Z'
---

## What

Wave 2 lane D of [[EPIC-011]]. Port `Pages/Search/Index.cshtml(.cs)` (moved from Cases/Index by PLAT-029) to `context.md` §1.7: advanced filter grid mapping 1:1 to the existing UI-07 inputs, results table with selectable rows (`tr[data-select-href]` + template preview, keyboard Enter/Space), server-rendered "Selected Case" pane for `?selected=` (facts, outstanding, Open Case, Copy Case/PO), "Closed · <outcome>" chip for non-design terminal states (D3).

## Owns

`src/Pegasus.Web/Pages/Search/**`, `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs`, `AdministrationSearchAccountWebTests.cs` (search parts).

Extended 2026-08-29 (round-2 review, scope-breach finding): `src/Pegasus.Core/Cases/CaseQueries.cs` and `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`, for the `CaseSearchItem` projection only. §1.7 draws a **Vehicle + make/model** results column and an **Accident circumstances** line in the selected-Case preview; neither fact is on the pre-port projection, so the contracted page cannot be drawn without them. The edit is three trailing optional constructor parameters plus their projection (commit `882f32ae`, disclosed as plan decision P2 — no new query, no migration, no behaviour change for existing callers). `waves.md` assigns those two paths to no other EPIC-011 lane (wave 3's Core lane owns `CaseTimeline.cs`), so there is no collision; the omission was in this Owns list, not in the code.

## Blocked by

[[PLAT-029]].

## Reported, not fixed (other lanes' files)

- [[PLAT-059]] — `Create Case` resolves to two destinations across four call sites; `Pages/Shared/_ShellDialogs.cshtml:64` and `wwwroot/js/site.js:1364` are [[PLAT-029]]'s.
- [[UIIMP-011]] — the two `cases--*` constants in `TestUiSnapshotTests.cs:28-29` still match pre-port markup; the file is [[UIIMP-005]]'s.

## Verification

- [x] Old `/Cases?query=` bookmarks 301 to `/Search` with the same values. Proved 2026-08-28 by `AdministrationSearchAccountWebTests.OldCasesSearchLinksRedirectToSearchWithTheirValuesIntact`: a thirteen-parameter bookmark, the 301 target asserted byte for byte, and every value rendered back into its field (PASS 6/6).
- [ ] No clipped text/overflow at 1580/1100/760. Needs a browser run; not done in the page lane, left for the orchestrator's walk.
