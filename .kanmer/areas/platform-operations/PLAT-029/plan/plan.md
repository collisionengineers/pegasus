# Plan — PLAT-029

Diff estimate: ~+3,200 / −2,900 lines (site.css replaced ~2,600 → ~1,900 new
+ ~1,400 legacy; site.js +600; layouts/partials ~400; sprite +45 symbols ×2;
routes ~300 moved; tests ~250; catalogue ~40).

## Steps (each a commit)

1. **Fonts + sprite.** Copy Inter 4.1 `InterVariable(.woff2/-Italic)` and
   `LICENSE.txt` to `wwwroot/fonts/inter/`. Grow `_LucideSprite.cshtml` and
   `lucide-sprite.svg` with 43 Lucide 0.344.0 symbols (paths from unpkg);
   keep the 17 existing. Record SHA-256s.
2. **site.css.** Flatten prototype layers in cascade order, final value only,
   `html[data-design="integrated"] X` → `X`, dead selectors dropped; add CSP
   utility classes; `@font-face` Inter; then `/* ==== LEGACY (wave 5 deletes) ==== */`
   carrying the old page-body rules (old tokens re-declared under
   `:root` with their original names — none collide with the new set except
   `--line`, `--muted`, `--ink`, `--radius`, `--shadow`, which the legacy
   block reads from the new palette). Shell rules and colliding base
   selectors (`.tabs`, `.btn*`, `.panel`, `.notice`, `.table-wrap`, `table/th/td`,
   `.metric-strip`, `.metric`, `.dropzone`, `.blocker*`, `.record`, `.stack`,
   `.mail-*`, `.admin-card`, `.upload-outcome`, `[hidden]`, `.sr-only`,
   `.eyebrow`, `.section-label`, `.field-hint`, focus) are not duplicated.
3. **Shell.** Rewrite `_Layout` (reuse `CurrentWhen`, `CountFor`,
   `inboxEnabled`), new `_ShellDialogs`, `_LayoutAuth`/`_LayoutExternal`
   → `.external-shell/.auth-card`, auth-family pages to the new card
   vocabulary, partials rewritten (data hooks preserved), site.js new
   delimited modules (generalised `[data-dialog]` reusing the existing
   reason-dialog focus trap, command palette, workspace tabs, shortcuts,
   row focus, sort toggle, preview, estimate tabs, range output, rail
   toggle, rotate, toasts).
4. **Labels + counts.** `OperatorLabels.CaseStage` D3 mapping + `Nav`
   constants; `_StatusChip` tones for "With Engineer"/"Complete"/"Closed · …";
   `RailCountsPageFilter` keys `Cases` (+ `ShellRenderedAtUtc`).
5. **Routes + catalogue.** `git mv` Cases/Index → Search/Index (route
   `/Search`), Triage/Index → Cases/Index (`tab`, `queue` alias, search-param
   redirect), Triage stub, Unidentified redirect, delete ImageIntake/Index,
   inbound link fixes, Administration index + `_AdminNav`, catalogue.json /
   index.html; run `Test-UiCatalogue.ps1`.
6. **Tests.** RailCounts, ShellAndStatusPage, Accessibility, new
   LayoutIntegrityTests; class/route touch-ups.
7. Build `dotnet build ./Pegasus.slnx --configuration Release`; merge
   `origin/dev`; simplification pass; report; PR.

## Reuse

- `CurrentWhen`/`CountFor` helpers, `OperatorLabels.OfficeClock`,
  `StaffSessionPolicy.IdleLifetime`, `StaffRoleNames`, existing focus-trap
  block, `BrowserTestSupport`, `IntakeWebDriver`, catalogue script.

## Simplification pass — 2026-08-28

Lenses run over `git diff origin/dev...HEAD` (reuse, simplification,
efficiency, altitude); `/simplify` not available in this agent, lenses
applied by hand.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | `_Layout` rendered `<main>` twice (with/without workspace attributes) behind a `@functions` helper. | Fixed — one `<main>`, Razor's null-attribute omission carries the optional `data-workspace-*`. |
| 2 | `.viewer-stage` declared twice in site.css (`position:relative` in a second rule). | Fixed — merged into one rule. |
| 3 | `_StatusChip` had a redundant `_ when key.StartsWith("closed")` arm before `_ => "neutral"`. | Fixed — dropped; comment states the D3 terminals read neutral. |
| 4 | Legacy `.stack { display:grid; gap:1px }` collided with the new `.stack`. | Fixed — removed from the legacy block (new rule wins). |
| 5 | `_MetricCard` repeats the tile body for `a` vs `div` hosts. | Accepted — Razor has no dynamic tag name; a second partial would be a heavier abstraction than 12 duplicated lines. |
| 6 | `LayoutIntegrityTests` enumerated `TheoryData` to reuse the route list. | Fixed — `AccessibilityTests.AuthenticatedRouteList` is the one list; both theories derive from it. |
| 7 | Triage-state words (`StateLabel`) still live on `Cases.IndexModel` rather than `OperatorLabels`. | Deferred — moving it touches `Triage/Details` and `Intake/Details` (wave-2 lane C2 files); left as a one-line reference fix and reported for C2. |
| 8 | `Pages/Index.cshtml`, `Triage/Details`, `ImageIntake/Details`, `Intake/Details` edited (one-line link/type fixes only). | Accepted — inbound-reference fixes named in the brief; page bodies untouched. |
| 9 | Route-string updates in tests outside the named 12 (`CasesIndexWebTests`, `QdosCustodialWebTests`, `ImageIntakeWebTests`, `AdministrationSearchAccountWebTests`). | Accepted — mechanical consequence of the route moves; without them the suite red-lines on merge. Listed in the report. |
