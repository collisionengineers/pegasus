# design-sync notes — Pegasus

Repo-specific facts for syncing `@pegasus/design-system` to claude.ai/design.

## Shape

- Pegasus is a .NET Razor Pages app with **no JS design system of its own**. The
  design system for Claude Design is `docs/design/system/` — a React package whose
  components render the exact markup and class names of
  `src/Pegasus.Web/wwwroot/css/site.css`, which the package build copies
  byte-for-byte to `dist/styles.css` (`docs/design/system/scripts/build.mjs`).
  Never author CSS in the package; if the app's stylesheet changes, rebuild.
- Markup patterns were taken from the Razor pages/partials (`_StatusChip`,
  `_FreshnessBanner`, `_Layout`, `_ReasonDialog`, `Cases/Details`, …). The
  state→tone map in `StatusChip` is a verbatim port of `_StatusChip.cshtml`.
- Icons: the 16-symbol Lucide sprite from `Pages/Shared/_LucideSprite.cshtml`,
  inlined as paths in `Icon.tsx`. No other icon set.
- Logo: `docs/design/brand/logos/logo_no_margin.png` downscaled to 416px
  (`docs/design/system/src/logo.png`, checksum in `docs/design/README.md`) and inlined as a
  data URI by esbuild.
- Build: `cd docs/design/system && npm run build` (esbuild ESM + tsc d.ts). Node 24.
- Converter: `--node-modules docs/design/system/node_modules --entry docs/design/system/dist/index.js`.
- Playwright: repo cache has chromium-1228 → `playwright@1.61.1` in `.ds-sync/`
  (1.62 wants 1234 and fails to launch).
- Windows/Git Bash gotcha: a heredoc containing an apostrophe breaks the Bash
  tool; write scripts/files with the Write tool and run them.
- **CRLF checkout churn (fixed 2026-08-16, watch for regressions):**
  `core.autocrlf=true` silently rewrites `.design-sync/previews/*.tsx` and
  `docs/design/system/src|docs/**` to CRLF on any fresh checkout/merge, changing
  their raw bytes and invalidating `sourceKeyFor`'s `hashFile` (no CRLF
  normalization) — this desyncs the remote anchor for most/all components on
  every Windows checkout even with zero real edits (confirmed via
  `renderHashes` identical throughout — pure bookkeeping churn, no visual
  change). Fixed by pinning `text eol=lf` for these paths in `.gitattributes`.
  If a resync ever again reports a large `changed` count with unchanged
  `renderHashes`, suspect this class of issue before mass re-grading: compare
  `ds-bundle/_ds_sync.json` renderHashes against the fetched remote anchor's
  — identical hashes mean it's safe to just re-confirm from the existing
  screenshots rather than treat every cell as freshly at-risk.

## Groups / docs / previews

- Per-component docs live in `docs/design/system/docs/<Name>.md`; frontmatter
  `category:` sets the DS pane group (Shell, Actions, Status, Metrics, Record,
  Data, Forms, Overlay, Auth, Layout). Body = usage guidance + examples (the
  `.prompt.md` the design agent reads); Props are appended automatically.
- `guidelinesGlob` points at `../README.md` from the package root (the design authority)
  so it ships as `guidelines/design.md`.
- All 81 previews are authored in `.design-sync/previews/` (no floor cards).
  Content is invented example data (case refs, registrations, insurer names).
- `cfg.overrides`: `site.css` reflows at ≤1279px (`.dashboard-grid`,
  `.split-main`, `.review-grid`, `.workbench-grid`, `.metric-strip`), ≤1023px
  (`.nav-inner`, `.page-heading`) and ≤900px (`.metric-strip`, `.proposal-diff`).
  The default 900×700 capture is below those, so wide shell/layout components
  carry `viewport: 1440x900` / `1400x700` / `1000x700` plus `cardMode: column`;
  `AuthShell` and `ReasonDialog` are `cardMode: single`. Changing an override
  clears that component's grades (re-look at the sheet).
- `ReasonDialog` previews use `inline` so the fixed backdrop does not cover the
  card; `Provenance.AllWords` adds preview-only captions under the icons.

## Known render warns (intentional)

- `[FONT_MISSING] Poppins`: only `.send-action` names it, and
  `docs/design/README.md` §Send to Claude says the face is *requested, never loaded*
  — no font bundle is ever added. Suppressed via `runtimeFontPrefixes:
  ["Poppins"]`; system fallback is what production renders too.
- `[DTS_STYLE_SYSTEM]` on every build: React DOM attribute props are filtered
  from `<Name>Props` — expected, the components extend `HTMLAttributes`.

## site.css fixes (2026-08-16)

- `--focus-ring` annotated `/* @kind other */` — Claude Design's self-check
  needs this to classify it (it's an outline shorthand, not a plain color).
  The companion "47 properties" finding (the `.queue-card--*`/`.status-chip--*`
  /`.status-card--*`/`.freshness-banner--*` variant blocks) was audited fully:
  every declaration is a `var()` reference to an existing `:root` token, none
  hardcode a literal, so per the self-check's own stated exception these are
  correct as authored — no code change made. If it keeps flagging them, that
  is expected (an info-tier note), not a bug to keep chasing.

## site.css fixes made by the first sync (2026-08-15) — review in the PR

All are contrast/layout defects in the app stylesheet that the previews
surfaced; each uses existing tokens only:

1. `.record__head .status-chip`: amber/red/green/neutral chip tones used 10%
   alpha tints that vanish on the dark band (~1.8:1). Now `--band-bright`
   ground, with `--navy-bg` / `--amber-bg` for the navy and amber tones.
2. `.auth-card label` rule added (the `.auth-panel` label rule was never
   carried over): small bold stacked labels; the password-change hint no
   longer runs into the label text.
3. `.page-heading`: `align-items: center` moved into the base rule so the
   ≤1023px column reflow (`flex-start`) is not clobbered by a later rule.
4. `.queue-list` rows: `grid-template-columns: 1fr 1fr auto auto` so a
   three-column mail row keeps its › on the same line.
5. `.queue-list > a > span, > article > span` (was `.queue-list span`), with
   `justify-items: start`, so a `StatusChip` inside a row keeps its compact
   inline pill shape instead of stretching to the column.

## App-CSS observations NOT fixed (report to the app owners)

- `.form-panel input[type="file"]` dashed drop-zone never applies — the global
  `input:not(...)` selector (0,3,1) outranks it (0,2,1).
- Inputs nested inside bold labels inherit `font-weight: 700` via `font: inherit`
  (Sign in, Case workflow forms).
- `.queue-filters a[aria-current="page"]` has no rule (the app never sets
  aria-current there either).
- `Pages/Cases/Assessment/Suggestions.cshtml` uses `<h4>` inside
  `.proposal-diff` but CSS styles `h3` (the DS renders `h3`).
- `.role-form > label` child combinator means a `Field` inside `RoleForm` is
  unstyled; the DS `RoleForm` preview uses a direct `<label>` like the app.
- `.evidence-list li` is a grid, so no bullets render despite the class name.

## Re-sync risks

- `dist/styles.css` is a copy of the app stylesheet — any `site.css` change
  needs a package rebuild before the converter or the bundle ships stale CSS.
- Preview data is invented example content in `.design-sync/previews/*.tsx`;
  nothing is read from the repo at build time.
- Hover/focus-only states (gated and provenance tooltips, hover fills, spin,
  Send-to-Claude ember canvas, sticky rails, skip-link focus) are not captured.
- The AppShell card shows a few px of the fixed skip link at the top-left (the
  transformed card wrapper is its containing block) — capture artefact only.
