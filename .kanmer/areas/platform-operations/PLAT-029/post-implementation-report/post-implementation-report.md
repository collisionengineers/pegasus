# Post-implementation report — PLAT-029

Branch `task/plat-029-workspace-shell` (worktree
`../pegasus-worktrees/plat-029-workspace-shell`), merged `origin/dev` at
690ca579. `dotnet restore --locked-mode` and `dotnet build --configuration
Release` are clean. Tests, snapshot regeneration and browser tests were not
run by the implementer (orchestrator wave loop).

## What shipped (commits on the branch)

- c73331de fonts + sprite · 9b8a1de8 site.css · 865b4c0c shell, partials,
  JS, labels, counts · b5ff6590 merge origin/dev · 71277763 routes, admin
  layout, catalogue · 646b6763 tests.
- **site.css** (2,487 lines): the prototype's five layers flattened to their
  final declarations; single `:root` with the integrated + polish tokens;
  no `data-design`; dead selectors dropped (`.baseline-workbench`,
  `.analyst-home`, `.console-status`, `.keymap`, `.prototype-note`,
  `.work-today-summary`, `.pane-layout--work*`, `.inspector*`,
  `.assessment-rail*`, `.assessment-details*`, `.assessment-v2*`,
  `.assessment-circumstances`, `.segmented`, `.combobox-*`, `.skeleton`,
  `.loading`, `.queue-layout--results`, `.table-action`, `.panel-foot`,
  `.grid-4`, `.divider`, `.dialog--wide`, the 1400px rule). Kept `.tabs/.tab/
  .tab-count`, `.kbd` (command results). Final values verified: queue-layout
  `185px minmax(0,.85fr) minmax(0,1.15fr)` with the first pane always shown;
  row selection `#f4f7fa` + `inset 4px 0 #263d56`; `.btn:disabled` opacity
  .45; 3px `--focus` ring. Utilities: `mt-0 mt-1 mb-2 ml-auto
  cluster--between cluster--start panel-body--compact panel-body--tight
  field--narrow no-border viewer-stage metric-strip--3 metric-strip--5
  rot-90/180/270 progress.progress text-small block link-plain`. Base
  `.icon` rule carries the stroke attributes (`<use>` instances do not
  inherit them from the sprite root) and `.sprite-sheet{display:none}`.
- **Fonts (D13):** Inter 4.1, `wwwroot/fonts/inter/`.
  SHA-256 (committed blobs):
  `InterVariable.woff2` 693b77d4f32ee9b8bfc995589b5fad5e99adf2832738661f5402f9978429a8e3;
  `InterVariable-Italic.woff2` e564f652916db6c139570fefb9524a77c4d48f30c92928de9db19b6b5c7a262a;
  `LICENSE.txt` (SIL OFL 1.1) 262481e844521b326f5ecd053e59b98c8b2da78c8ee1bdbb6e8174305e54935a.
- **Sprite:** 17 existing + 43 Lucide 0.344.0 symbols in both
  `_LucideSprite.cshtml` and `wwwroot/images/lucide-sprite.svg` (60 each).
  SHA-256 (committed blobs, LF): `lucide-sprite.svg`
  90feb7ab7e40931dde9b011cec06f4e8b4dcd058695dec09db5e0965ac7a0992;
  `_LucideSprite.cshtml` 47fec3d1b9566d6ac0bca4e7e4b4f36e298d0d3d8e51e0c113ee1036cf6622ca.
  The repo's autocrlf will re-line-end these on checkout; the design README
  row should cite the blob (`git show HEAD:<path> | sha256sum`).
- **Shell:** `_Layout` per §1.1 (rail: brand, Work/Manage labels, Work
  Centre · Inbox · Upload · Cases [count] · Search · Operations ·
  Administration; health line and user block; utility bar with search form
  to `/Search`, Add and Notifications; workspace-tab strip; `main.app-main >
  .content`; `_ShellDialogs`; toast region). Rail glyphs per the design
  README (Cases `list`, Search `folder-open`). `_LayoutAuth`/`_LayoutExternal`
  → `.external-shell > main > section.auth-card > .auth-brand`. Auth family
  pages (SignIn, AccessDenied, PasswordChange, Error, StatusCode,
  Connect/Authorize) on the new vocabulary; test-pinned copy kept ("Sign in
  to Pegasus", "You are signed out", "We could not find that page" comes
  from the model). `.rail-user` keeps `role=group aria-label=User` (an
  existing browser test reads it).
- **Partials:** `_PageHeader` (`header.page-header > .page-title + .page-actions`),
  `_MetricCard` (`a.metric[data-value]`/`div.metric`, absent → `.metric-value.muted` "—"),
  `_StatusChip` (`span.status.status--<tone>`; "With Engineer" navy, "Complete" green,
  "Closed · …" neutral), `_FreshnessBanner` (`.freshness` dot + "Current · HH:MM",
  status chip for stale/failed, `data-refresh-form` GET kept), `_ReasonDialog`
  (`.dialog-backdrop[data-dialog][data-reason-dialog]`, form contract and
  `Reason`/`maxlength=500`/"Confirm Action" kept), `_EvidenceViewer`
  (dialog + `.viewer-stage`, all `data-evidence-*` hooks, Rotate button),
  `_ImageGallery` (`.gallery/.gallery-item/.gallery-image/.gallery-caption`,
  `data-evidence-set/item` kept), `_UploadOutcome` (`.upload-outcome`, all
  `data-case-search*` hooks kept).
- **site.js:** the reason-dialog block is now the generalised
  `[data-dialog]` block (`data-dialog-open`, `data-dialog-close` +
  `data-dialog-dismiss` alias, Escape, backdrop click, focus trap, `inert` on
  `[data-app-shell]`, focus return); evidence viewer sets `inert` too. New
  delimited sections: toasts (+ `[data-confirmation]` announce), command
  palette (filter, arrows, Enter, fallback → `/Search?query=`, Ctrl K, Enter
  in `[data-command-input]`), workspace tabs (`localStorage
  pegasus.workspaceTabs`, max 4 LRU, `main[data-workspace-record]` from
  `ViewData["WorkspaceRecord"] = (Href, Label)` tuple), shortcuts (Ctrl K/U/N/S,
  F5), `[data-row-list]` roving focus, `[data-sort-toggle]`,
  `[data-select-href]` preview with `<template>` + `history.replaceState`,
  `[role=tablist]` roving tabindex, `input[type=range][data-range-output]`
  (+ `data-range-base`/`data-range-amount-output`), `[data-rail-toggle]`,
  `[data-rotate]` → `[data-rotate-target]`. PR #581's heartbeat block has not
  merged yet; my additions are at the end of the file.
- **Labels/counts:** `OperatorLabels.CaseStage` D3 mapping; `Nav`, `Admin`,
  `Freshness` constant classes; `StaffRole`, `Initials`, `Duration`.
  `RailCountsPageFilter` → keys `Inbox|Cases|Operations`, only `Cases`
  populated (NotReady+Review+Held), plus `ShellRenderedAtUtc` from
  `TimeProvider`.
- **Routes:** `Triage/Index` → `Cases/Index` (`/Cases`, `tab` with `queue`
  alias; any search-only parameter 301s to `/Search` with the query string);
  `Cases/Index` → `Search/Index` (`/Search`); `/Triage[?queue=]` 301 →
  `/Cases[?tab=]`; `/Unidentified` 301 → `/Cases?tab=unidentified`;
  `ImageIntake/Index` (`/VehicleImages` list) deleted (D1), detail page kept
  with its back link → `/Cases?tab=not_ready`. Inbound one-line fixes:
  `Pages/Index.cshtml` metric links, `Triage/Details` crumb + `StateLabel`
  reference, `Intake/Details` `StateLabel` reference, `ImageIntake/Details`.
  `Administration/Index` renders the admin-layout with the new
  `Administration/Shared/_AdminNav` (Accounts, Principals, Configuration,
  Mailboxes, Automation when composed; Service health/Action Logs/Reports
  omitted until their pages exist).
- **Catalogue:** `catalogue.json` and `index.html` updated (sources moved,
  `/Triage` and `/Unidentified` classified redirect, `vehicle-images--*`
  entries and files removed; scenario ids unchanged so the snapshot script
  keeps working).
- **Tests:** `RailCountsWebTests` (Cases count regex, no count on
  Inbox/Operations), `ShellAndStatusPageWebTests` (Work Centre/Cases/Search,
  `aria-label="Primary"`, `auth-card`), `AccessibilityTests` (route list as
  `AuthenticatedRouteList`; `/Cases`, `/Cases?tab=triage`,
  `/Cases?tab=unidentified`, `/Search`; `.app-shell > .app-rail` first-paint
  check; metric selector accepts both spellings until lane A), new
  `Browser/LayoutIntegrityTests` (routes × 1580/1100/760: no horizontal
  overflow, no clipped text outside the allow-list, one main, one h1, no
  `[style]`). Touch-ups: `OperatorJourneyTests` (nav order, `/Cases?tab=review`),
  `UploadCaseSearchBrowserTests` (`[data-confirmation]`),
  `AssessmentReadinessSummaryBrowserTests` (`.status`), `TriageQueuesWebTests`,
  `CasesIndexWebTests`, `QdosCustodialWebTests`, `ImageIntakeWebTests`,
  `AdministrationSearchAccountWebTests` (route strings).

## Legacy block contents

`/* ==== LEGACY (wave 5 deletes) ==== */` at the end of site.css: the old
non-colliding tokens (`--ce-red`, `--charcoal`, `--paper`, `--panel`,
`--band*`, `--amber-fg/tint/line`, `--navy-fg/line`, `--success*`, `--sp-*`,
`--t-*`, `--radius-sm`, `--border`, `--focus-ring`, `--shadow-soft`,
`--scrim`, `--shadow-modal`, `--state-*`), the `[data-state]` channel,
queue cards/metric tiles, dashboard/split/review grids, detail/plain/evidence
lists, field cards, queue filters/list, pager, admin-workspaces,
primary/secondary-action, form-panel/form-grid, `.metric__label/__value/
__absent`, `.metric-strip--secondary`, `.status-chip*`, `.status-card*`,
acceptance-boundary, validation errors, back-link, role forms, failure
detail, freshness-banner, reason-dialog(-backdrop), workbench/send-action,
section-tabs, readiness summary, evidence figures, proposal diff, line-grid,
record__ BEM parts, `.btn--light/--icon`, `.tabs a/.count`, subtabs, prov,
gated, facts, datarow, blockhead, refresh, status-card--done, row-confirm,
crumb, filterbar, mail-workspace/preview parts, dropzone parts,
upload-layout/steps/outcome-list/attach, case-search-list, form-column,
block-grid/block, marks, est-tabs, damage plan/grid, image-gallery,
upload-thumb, filter-row, th[aria-sort], decision, mail-from/quoted/body.
Not carried (new rule wins): `.panel`, `.notice`, `.btn*`, `.tabs`,
`.table-wrap/table/th/td`, `.metric-strip/.metric`, `.dropzone`, `.blocker*`,
`.record`, `.stack`, `.mail-preview/.mail-route/.mail-body`, `.admin-card`,
`.upload-outcome`, `.accepted-list`, `.eyebrow/.section-label/.field-hint`,
`.sr-only`, `[hidden]`, focus, `.page-heading`, `.auth-*`, `.app-nav`,
evidence-viewer__, `label.req`. No `.legacy-` renames were needed.

## What wave 2 must know

- Page bodies still emit `.page-heading`, `.metric__value`, `.status-chip`,
  `.primary-action`, `.reason-dialog-backdrop` etc.; the legacy block renders
  them. Each lane ports its markup to the new vocabulary and the partials.
- `ViewData["WorkspaceRecord"] = (Href, Label)` on a Case page adds it to
  the workspace-tab strip. `ViewData["AdminArea"]` on an admin page marks
  `_AdminNav`'s current item.
- `_StatusChip` renders `.status` (dot + text, no icon). `_FreshnessBanner`
  renders `.freshness`; put it in `.page-actions`.
- The Cases page still renders the legacy tab strip; §1.4's three-pane
  `queue-layout` is lane C1. `/Cases` accepts `tab=` (and `queue=`).
- `OperatorLabels.Nav/Admin/Freshness` are the label lists; `CaseStage`
  now returns D3 words — pages comparing stage text must use the enum.
- Legacy `StateLabel` for Triage states still lives on
  `Pages.Cases.IndexModel` (referenced from `Triage/Details` and
  `Intake/Details`); lane C2 should move it to `OperatorLabels`.

## Open questions / not done

- `scripts/Test-UiCatalogue.ps1` fails on `Administration/Principals/
  EvaSubmission.cshtml` "not classified" — a pre-existing gap from TICK-077
  (#574) that is absent from origin/dev's catalogue too; a visual entry needs
  its snapshot generated (orchestrator), so it is not added here.
- Inbox/Operations counts stay absent (wave 3). Notifications dialog renders
  the empty state (no source).
- Account dialog shows Name/Role/Idle lock; "Session started" awaits the
  `auth_time` claim.
- `Administration/{Roles,Access,Organizations}` pages still exist and are
  listed in `AuthenticatedRouteList` until their folding tickets land.

## Review of PR #589 — changes applied (2026-08-28)

1. **inert (blocking):** `site.js` no longer sets `inert` on `[data-app-shell]`.
   `inertOutside(dialog)` walks from the dialog to `body` and sets `inert` on
   every sibling of each ancestor, recording them; close releases exactly
   those. Used by the generalised `[data-dialog]` block and the evidence
   viewer; `_ShellDialogs` unchanged. Browser assertion added in
   `OperatorJourneyTests` (evidence tab, remove-document reason dialog:
   visible, focus inside, not under `[inert]`, Confirm enabled, Cancel
   real-clicked, no `[inert]` left).
2. **Cases rail count = contract sum:** `CaseStageCounts` gains
   `WithEngineer` (ReportPreparation + PostReport) in Core, counted by
   `EfDashboardQueries`; `RailCountsPageFilter` sums
   not_ready + review + with_engineer + held + open Triage
   (`IListTriage`, page size 1, `TotalCount`) + Unidentified
   (`IUnidentifiedStore.ListQueueAsync(null)`), all in one `Task.WhenAll`,
   actor from `StaffActorFactory`.
3. **README D13 rows:** font SHA-256s, licence SHA-256, sprite checksum line
   (committed-blob SHA) and all 43 `pending` glyph rows filled with the
   SHA-256 of each `<g id="icon-…">…</g>` element (the existing rows' method,
   verified against `upload`).
4. `--font` is now exactly the README stack; `@font-face` family stays `Inter`.
5. `--polish-shadow` inlined into its 7 rules; `--polish-shadow-raised`,
   `--polish-red-soft`, `--polish-blue-soft` deleted (no callers).
6. **Reviewed divergences (recorded):** account dialog keeps a "Change
   password" link so `/Account/PasswordChange` stays reachable (prototype
   had only Close/Sign out); Add dialog omits "Create upload request" (needs
   a Case picker — wave 4); utility search has no placeholder (no
   explanatory copy); freshness reads "Current · HH:MM" (prototype "All
   sources current"), stale/failed states as a status chip.
7. `.status--neutral` keeps the prototype `#57534e` on `#f3f2f0`.
   **Docs follow-up:** README §Status chips row for `.status--neutral`
   (`--muted` on `--surface-3`) must be corrected to those values.
8. Evidence viewer uses `.dialog--wide` (rule reintroduced); the
   `.viewer-stage[aria-busy]::after` rule exists once; `.text-small`,
   `.block`, `.link-plain` removed (no callers); `.rot-*` and `progress` kept.
9. `catalogue.json`/`index.html` branch text for `Administration/Index`
   corrected. **`docs/current-architecture.md` is DELIV-030's** — the shell,
   route and count changes here need reflecting there (rail routes,
   `/Cases`/`/Search`/`/Triage` stub, `RailCountsPageFilter` queries).
10. `LayoutIntegrityTests` allow-list adds `.rail-user strong` and
    `.workspace-tab span`.
