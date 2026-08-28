---
id: PLAT-029
type: ticket
title: >-
  Deliver the Integrated Operations Workspace shell, design system and route
  structure
status: preparing
area: platform-operations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-28T08:12:48.789Z'
labels:
  - ui
  - information-architecture
  - operator-requested
  - design-system
  - shell
groups:
  - EPIC-008
  - EPIC-011
links: []
docs_todo: true
archived: false
created: '2026-08-21T13:19:14.464Z'
updated: '2026-08-28T08:12:48.789Z'
---

## What

Wave 1 of [[EPIC-011]]: replace `site.css` wholesale with the Integrated Operations Workspace system (final prototype layer only, plus a delimited legacy block for not-yet-ported page classes), rewrite the authenticated shell (`_Layout`: 220px rail, dark utility bar, workspace-tab strip, 1580px centred content; `_LayoutAuth`/`_LayoutExternal` → external shell + auth card), grow the Lucide sprite to the prototype's icon set, vendor Inter Variable (D13), add the shell JS modules (generalised dialogs with `inert`, command palette, Add/Notifications/Account dialogs, workspace tabs in localStorage max 4 LRU, shortcuts, arrow row navigation, sort toggles, row-selection preview, estimate tabs, range output, rail collapse, image rotate classes, toasts), restructure routes (`/Triage`→`/Cases` queues, `/Cases`→`/Search`, 301 stubs, delete the `/VehicleImages` list), rewrite the shared partials, `OperatorLabels` (D3 labels, nav labels) and `RailCountsPageFilter` (keys Inbox/Cases/Operations; absent until real figures exist), and update the shell/route/accessibility tests plus a new `Browser/LayoutIntegrityTests` (1580/1100/760: no overflow, no clipped text, no inline style).

## Why

Every page port in wave 2 depends on the shared stylesheet, shell and routes; they are indivisible because splitting CSS from markup would leave `dev` broken between merges.

## Owns

`src/Pegasus.Web/wwwroot/css/site.css`, `wwwroot/js/site.js`, `wwwroot/fonts/inter/**`, `wwwroot/images/lucide-sprite.svg`, `Pages/Shared/**`, `Pages/_ViewImports.cshtml`, `Presentation/OperatorLabels.cs`, `Presentation/RailCountsPageFilter.cs`, route moves/stubs (`Pages/Triage/Index.*`, `Pages/Cases/Index.*`, `Pages/Search/Index.*`, `Pages/Unidentified/Index.*`, delete `Pages/ImageIntake/Index.*`), `Pages/Administration/Index.cshtml` (admin-layout + `_AdminNav`), `Pages/Account/**`, `Pages/Error.*`, `Pages/StatusCode.*`, `Pages/Connect/**`, tests `RailCountsWebTests`, `ShellAndStatusPageWebTests`, `Browser/AccessibilityTests`, new `Browser/LayoutIntegrityTests`, class-name touch-ups in the 12 class-referencing test files, `docs/design/test-ui/catalogue.json`.

Page bodies keep their current markup (rendered by the legacy block) — page content is wave 2.

## Verification

- [ ] Shell matches EPIC-011 `context.md` §1.1 and §1.13 at 1580/1100/760 with no clipped text or overflow.
- [ ] All routed pages remain reachable; old URLs 301 to the new ones; `scripts/Test-UiCatalogue.ps1` passes.
- [ ] Rail counts only where a real figure exists; none invented.
- [ ] Operator eyeballs the shell before wave 2 starts.

## Outcome
