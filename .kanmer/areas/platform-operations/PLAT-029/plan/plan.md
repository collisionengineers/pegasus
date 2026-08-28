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
