# Research — Claude Design UI implementation

## Source

Claude Design project `710bb42f-84ed-4d82-b216-7c5d60fb5aef` ("Pegasus Design"),
read through the `DesignSync` MCP. 21 screen prototypes, one screen-local
scaffolding file, and an embedded copy of the design system
`pegasus-design-system-d795cfe7-300d-4c42-a837-9a1714dd1a36`.

The project's own `github.md` records `repo: collisionengineers/pegasus`, a last
sync of 2026-08-16, and — decisively — a **screen map** from every prototype to
the Razor pages it was built from. Its note says the screens were "rebuilt …
**from** the live Pegasus.Web Razor pages". This is therefore a round trip, not a
greenfield design: the prototypes restate our own markup with refinements, and
implementing them means folding those refinements back.

## Finding 1 — there is no CSS work

`_ds/…/_ds_bundle.css` (62,714 chars) was diffed against
`src/Pegasus.Web/wwwroot/css/site.css`. The entire diff is **two lines**, and both
are stale comment references:

```
< …approved set in docs/design/README.md   (repo — current)
> …approved set in docs/design.md          (design project — stale path)
```

`styles.css` is a one-line `@import "./_ds_bundle.css";`. So the design system's
stylesheet *is* our `site.css`, and the repo copy is the newer of the two. No
token, no component rule, and no colour changes. Everything in this ticket is
markup, plus the one new pattern in Finding 3.

## Finding 2 — the component-to-DOM translation table

`_ds_bundle.js` carries the real render code for all 81 components. Extracted the
exact DOM each emits, because that is what the Razor has to produce. The ones
these screens use:

| Component | Emitted DOM |
| --- | --- |
| `Panel` | `<section class="panel">` |
| `PageHeading` | `<header class="page-heading">` → `<h1>`, or `<div><p class="eyebrow"><h1></div>` when `eyebrow`; `actions` → `<div class="page-heading-actions">`; `refresh` rendered bare |
| `SectionLabel` | `<h2 class="section-label">`, `section-label--iconed` with an icon |
| `Eyebrow` / `Lede` / `EmptyState` | `<p class="eyebrow">` / `.lede` / `.empty-state` |
| `StatusCard` | `variant="done"` → `<p class="status-card status-card--done" role="status">` + check-circle icon + `<span>`; otherwise `<section class="status-card status-card--{variant}">` + `<h2>` + `<p>` |
| `Notice` | `<aside class="notice">` wrapping `<p style="margin:0">` |
| `Provenance` | `<span class="prov" data-word="…" tabindex="0" role="img" aria-label="…">` + icon |
| `DataRow` | `<div class="datarow">` → `.datarow__field`, `.datarow__value` (or `.datarow__value.quiet` = "Not recorded"), `.datarow__sug` ("Suggested …"), `.datarow__end` |
| `DetailList` | `<dl class="detail-list">` → `<div><dt><dd>` |
| `FormGrid` / `Field` | `<div class="form-grid">`; field = `<div>` (`.field-wide` when wide) → `<label for>`, control, `<small class="field-hint" id="{for}-hint">`, `<span class="field-validation-error" id="{for}-error">`, wired by `aria-describedby` / `aria-invalid` |
| `Choice` / `ChoiceGroup` | `<label class="choice"><input><span>`; `<fieldset class="role-choices">` (+`--stacked`) with `<legend>` |
| `Button` | `<button class="btn">` or `<a class="btn">`; `btn--dark`, `btn--light`, `btn--icon`; `disabled` + `condition` wraps in `<span class="gated" data-condition>` |
| `PrimaryAction` / `SecondaryAction` | `<button class="primary-action">` / `.secondary-action`, or `<a>` with `href` |
| `ButtonRow` | `<div class="button-row">` (+`--end`) |
| `DataTable` | `<div class="table-wrap"><table>` + `<caption class="vh">` unless visible; `tabular` cells get `class="tabular"`; empty rows → `<p class="empty-state">` |
| `Pager` | `<nav class="pager" aria-label>` → Previous / `.pager__context` / Next |
| `FilterBar` | `<section class="panel filterbar">` + `<h2 class="vh">` + `<form>` |
| `Refresh` | `<div class="refresh" role="status" aria-live="polite">` + `Updated <time>` + status chip |
| `Metric` | `<a class="metric" data-state>` → `.metric__label` (icon + label) + `.metric__value` |
| `Gated` | `<span class="gated" data-condition="…">` |
| `RowConfirm` | `<details>` → `<summary class="btn">` + `<form method="post">` |
| `FormPanel` | `<section class="panel form-panel">` + title + `<form class="form-grid">` |
| `SplitMain` / `AdminWorkspaces` / `WorkbenchGrid` | `.split-main` / `.admin-workspaces` / `.workbench-grid` |

Every one of those class names already exists in `site.css` (Finding 1), and the
app already ships matching partials: `_PageHeader`, `_StatusChip`, `_Provenance`,
`_MetricCard`, `_ReasonDialog`, `_FreshnessBanner`, `_ErrorSummary`.

## Finding 3 — the one genuinely new pattern: the left rail

`screens/shared.jsx` does **not** use the design system's `AppShell`/`AppNav`. It
defines its own `Shell`: a 236px sticky left sidebar (`display:grid;
grid-template-columns:236px minmax(0,1fr)`), white ground, brand lockup at the
top, the seven routes as `.rail-link` rows, per-route counts, and the signed-in
identity / change-password / sign-out block pushed to the bottom with
`margin-top:auto`. `main` is `padding:20px 24px 32px; max-width:1280px`.

The current route is `aria-current="page"` styled with a 2px red left border, a
red-tint ground, red text and `font-weight:700`.

This is prototype-local CSS held in an inline `<style>` block — it is **not** in
`_ds_bundle.css`, and therefore not in `site.css`. It is the only new stylesheet
work in the ticket. The app today renders `header.app-nav > .nav-inner >
.nav-links` (a top bar) from `_Layout.cshtml`.

`docs/design/README.md` (line 470) requires the current route to carry
`aria-current="page"` "with a weight and underline change so it is not signalled
by colour alone". The rail keeps `aria-current` and the weight change but
replaces the underline with a left border — a non-colour signal of equal
standing, but a divergence from the text as written, so the authority file has to
say so.

`screens/UploadLink.html` is the exception: it uses the design system's real
`AppShell` + `AppNav brandOnly`, which matches our existing `_LayoutExternal`.
`screens/ChangePassword.html` uses `AuthShell` + `AuthCard`, matching
`_LayoutAuth`. So only the authenticated shell changes.

## Finding 4 — deferred capabilities appear in the prototypes

`screens/Assessment.html` and parts of `screens/Case.html` show capability the
repository has deliberately **not** allocated to this release. Checked against
`docs/capabilities.md`:

| Prototype feature | Capability | Allocation |
| --- | --- | --- |
| Progressive Engineer workbench | `UI-15` | **Later / 1.0.0** — "arrangement only" |
| Multi-estimate tabs, estimate lines, totals | `EXT-09` | **Later / 1.0.0** — "formulas and permissions require accepted authority" |
| Versioned valuation evidence, Engineer's value | `EXT-10` | **Later / 1.0.0** |
| Import assessment PDF / Open in Audatex | `EXT-12` | **Later / 1.0.0** — "blocked pending accepted PDF variants" |
| Glass's / CAP HPI / Cazana valuation figures | `EXT-13` | **Later / 1.0.0** — "access/terms and each adapter require separate acceptance" |
| Experian AutoCheck vehicle-history check | *(none)* | **not in the inventory at all** |
| Engineer queries, upload-link copy dialog, damage marking, report-image ordering | *(none)* | not allocated |

`docs/design/README.md` line 65 is explicit that capabilities beyond
`0.1.0-alpha.1` carry "no alpha navigation, control, workflow or placeholder".

Against that, the repository has already set a **precedent for exactly this
situation**. `Pages/Cases/Assessment/Index.cshtml.cs` says in its own summary:

> "This model binds the case identity header, the Send to Claude panel, and the
> PAV slider's recorded-evidence data; **the section forms themselves stay
> unbound design markup until the UI-15 activation task wires the staff save
> paths.**"

So Assessment is *already* shipped as unbound design markup awaiting UI-15. The
operator decision on this ticket (2026-08-17) is to extend that precedent: render
the deferred sections as static, unbound markup with no page-model binding and no
working POST handler, rather than either omitting them or inventing Core
behaviour. Recorded in `open-questions/`.

## Finding 5 — the PNG icon marks are prototype scaffolding, not design system

The prototypes reference 14 PNGs under `assets/icons/` through `Mark`,
`MarkLabel` and `MarkEmpty` — helpers defined in `screens/shared.jsx`, i.e.
screen-local scaffolding, **not** components of the design system.

The design system's own README contradicts them: *"Icons come only from `Icon`
(16 Lucide glyphs: search, user, refresh-cw, clock, calendar, check-circle,
alert-triangle, alert-circle, info, file-text, filter, shield, chevron-right,
arrow-right, upload, lock)."*

The repository agrees and is stronger: `docs/design/README.md` carries a
checksummed approved-asset register of exactly those 16 Lucide glyphs, states
"No brand or decorative imagery is needed for the internal Web application", and
prohibits "decorative or generated replacement icons". The app ships them as
`wwwroot/images/lucide-sprite.svg` behind `_LucideSprite.cshtml`.

Two authorities and the design system's own README all say Lucide; only the
prototype scaffolding says PNG. **Resolution: keep the approved Lucide sprite**
and use the existing brand asset `wwwroot/images/logo_no_margin.png` for the rail
lockup. No new binary assets, no register changes. Recorded as a divergence.

## Finding 6 — per-screen delta

Worked example, `screens/CreateCase.html` vs `Pages/Cases/Create.cshtml`:

- H1 becomes the source filename ("From EREF19-instruction.eml") and the `lede`
  disappears — the design system requires "a single H1 — no lede, no subtitle".
- New `StatusCard variant="done"`: "File read — confirm the extracted values below."
- Details panel moves from the `_InstructionDraftFields` partial + an explanatory
  paragraph to `DataRow` + `Provenance` rows, with a `Change a value` button.
- The image-based-assessment paragraph becomes a `Notice` (amber `<aside>`).
- The Case panel's stacked `<label>`/`<input>` pairs become a `FormGrid`, and the
  **separate Evidence completeness panel folds into the Case panel** as a stacked
  `ChoiceGroup` — five panels become four.
- Reason gains a `ButtonRow` with `PrimaryAction` + a `SecondaryAction` Cancel.
- Content is constrained to `max-width:860px`.

The same shape of change repeats across the set: fold the lede into the H1, move
stacked form fields into `FormGrid`/`Field` with hints and error ids, replace
loose paragraphs with `Notice`/`StatusCard`, put row actions behind `RowConfirm`,
give tables a `Pager` and a caption, and give zero results an `EmptyState`.

## Bearing on FRD-12

The governing FRD requires "clear counts that link to their exact filtered work",
"exact state labels mapped to Core decisions" and the full state set (loading,
empty, current, stale, unavailable, partial, failed, validation, conflict,
access-denied). The prototypes are consistent with all of it — `Refresh` carries
`updatedAt` plus a status chip, `Metric` links to its filtered queue, and
`StatusChip` takes settled state text. Nothing in the design contradicts the FRD;
the rail is a shell change the FRD does not speak to.

## Bearing on architecture

Presentation only. No file outside `src/Pegasus.Web` is touched, no business
policy moves out of `Pegasus.Core`, no new project, store or deployment unit.
The deferred sections are markup with no page-model binding, so they add no
capability and no policy owner.
