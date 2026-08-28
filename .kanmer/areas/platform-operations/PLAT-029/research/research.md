# Research — PLAT-029 shell, design system, routes

## Verified by read-only checks (2026-08-28)

- Worktree `../pegasus-worktrees/plat-029-workspace-shell` is on
  `task/plat-029-workspace-shell` at `origin/dev` (783b4b88), clean.
- Prototype `~/Downloads/Pegasus_UI_Assessment_Refined.html`: five style
  layers at lines 9–172 (base + `html[data-design=…]` overrides),
  182–580 (`pegasus-light-polish`), 582–774 (`pegasus-assessment-refinement`),
  775–786 (`annotation-neutral-row-focus`), 788–808
  (`pegasus-refined-final-style`). Final `navItems` at line 1648; `iconPaths`
  826–851; `dialogFrame`/`commandItems`/`renderDialog` 1150–1175;
  `handleKeydown` 1316; scope-icon polish script 2142–2205. Lines 814–817 and
  1378 (base64) were not read.
- Integrated tokens confirmed at prototype line 37–46: `--bg #f1f3f4`,
  `--ink #202629`, `--nav #24282b`, `--red #c9222b`, `--rail 220px`,
  `--content-max 1580px`, `--gap 12px`, `--page-pad 18px`, `--row 40px`.
- Current `site.css` is 2,583 lines; shell rules occupy 200–438 (`.app-nav`,
  `.app-rail*`, `.rail-link*`, `.skip-link`), 444–455 (`.page-heading`),
  1002–1016 (focus), 1037–1056 (rail reflow), 1825–1839 (`.auth-shell`,
  `.auth-card*`), 2268–2271 (`.app-rail__brand .mark`). Everything else is
  page-body styling and becomes the LEGACY block.
- Class references in tests: `rail-link` 4, `status-card` 2,
  `page-heading` 1, `metric__` 4 (AccessibilityTests, OperatorJourneyTests,
  DashboardCountersWebTests, TriageQueuesWebTests); `auth-shell`,
  `app-rail__`, `queue-card`, `metric-card` 0.
- Route hits in tests outside the owned list: `TriageQueuesWebTests`
  (`/Triage?queue=…`, asserts `/Unidentified` → `/Triage?queue=unidentified`),
  `ImageIntakeWebTests` (`/VehicleImages?query=`), `AdministrationSearchAccountWebTests`
  (asserts `/Search` 301 → `/Cases`), `OperatorJourneyTests`
  (`/Triage?queue=review`), `UploadOutcomeQueriesTests` (`/VehicleImages/{id}`
  detail — stays valid). Test client uses `AllowAutoRedirect = false`.
- `RailCountsPageFilter` populates only `Queues` = NotReady+Review+Held via
  `IDashboardQueries.GetCaseStageCountsAsync`. No Inbox/Operations figure exists.
- `StaffSessionPolicy.IdleLifetime` = 2 h (Core/Actors) — the idle-lock value.
- Sprite: `_LucideSprite.cshtml` (17 `<symbol>`s) and
  `wwwroot/images/lucide-sprite.svg` (17 `<g>`s with an inline
  `style="display:none"`) are parallel copies; the design README records the
  svg SHA-256. Both grow together.
- Lucide 0.344.0 SVGs fetched for all 43 new names (unpkg lucide-static).
- Inter latest release is v4.1 (`Inter-4.1.zip`), contains
  `web/InterVariable.woff2`, `web/InterVariable-Italic.woff2`, `LICENSE.txt`.
- PR #581 (`origin/task/case-024-edit-lease-heartbeat`) adds
  `_EditHeartbeat.cshtml`, `_EditFinishConfirm.cshtml` and a site.js block;
  my site.js additions go in delimited new sections at the end.
- Existing dialog contracts: `[data-reason-dialog]` div backdrops opened by
  `[data-dialog-open=<id>]`, closed by `[data-dialog-dismiss]`; native
  `<dialog data-focus-trap>` + `[data-dialog-close]` also exist (Cases). Both
  are kept working.
- `Pages/Index.cshtml` (lane A, wave 2) links `/Triage/Index?queue=…` from
  its metric tiles; `ImageIntake/Details`, `Triage/Details`, `Cases/Details`
  and `Presentation/UploadOutcome.cs` reference `/ImageIntake/Index`,
  `/Triage/Index`, `/Cases/Index`.

## Assumptions

- Page bodies keep `.metric__value`, `.page-heading`, `.status-card` etc.;
  the legacy block renders them. Tests that pin those page-body classes stay
  as they are; shell-class tests change.
- Inbox/Operations counts stay absent (no query); wave 3 extends.
- The account dialog shows Name / Role / Idle lock (no `auth_time` claim exists yet).

## Design authority cited (after merging origin/dev at 690ca579)

`docs/design/README.md` (UIIMP-006, PR #587) is the authority implemented
against: §Tokens (Colour, Typography, Shape/borders/focus — 3px `--focus`
ring, Spacing/layout/breakpoints 1360/1180/1100/980/900/760), §Icons (rail
glyphs: Work Centre `layout-dashboard`, Inbox `inbox`, Upload `upload`,
Cases `list`, Search `folder-open`, Operations `loader`, Administration
`layout-grid`; the font table rows at lines 371–374 are placeholders
"Recorded by PLAT-029"), §Component map › Utility classes (the exact CSP
class names), §Routes, §Workspace contract §Cases/§Search/§External frames.
The README's font and sprite SHA rows are UIIMP-006's file to fill from the
post-implementation report.
