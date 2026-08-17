# Pegasus design system — how to build with it

Pegasus is Collision Engineers' internal case-management tool: an operational, restrained, desktop-first UI for ~8 staff. Every component here renders the app's real markup and classes; the look comes entirely from `styles.css` (the app's own `site.css`), which must be loaded once. There is no provider, theme object or JS setup — import from `window.PegasusDS`, load `styles.css`, done.

## Page frame

Wrap every authenticated screen in `AppShell` with an `AppNav` (routes in the settled order: Dashboard, Inbox, Upload, Queues, Cases, Operations, Administration). Inside `main`, a screen is: one `PageHeading` (a single H1 — no lede, no subtitle; the freshness `Refresh` element sits in its `refresh` slot) then content. Navless surfaces (sign-in, error, not found) use `AuthShell` + `AuthCard`. A screen about one record is ONE `Record` container: `RecordHead` (dark band: reference, identity, `StatusChip`), `RecordBar` (actions valid now; the committed action in `end`), then `Tabs` + `RecordBody` or a plain `RecordBody`. Put a `Crumb` above it.

## The styling idiom

No utility classes and no CSS-in-JS. Style through the components' own props; for your own layout glue use the layout components (`Panel`, `DashboardGrid`, `SplitMain`, `ReviewGrid`, `WorkbenchGrid`, `FormGrid`, `ButtonRow`, `MetricStrip`, `QueueGrid`) or inline style using the app's tokens: spacing `var(--sp-1)`=4px, `--sp-2`=8, `--sp-3`=10, `--sp-4`=12, `--sp-5`=14, `--sp-6`=16, `--sp-8`=20, `--sp-10`=24; colours `--ce-red`, `--ce-red-dark`, `--charcoal`, `--ink`, `--paper`, `--panel`, `--line`, `--muted`, `--amber-fg/--amber-bg/--amber-line`, `--navy-fg/--navy-bg/--navy-line`, `--success`; type `--t-h1`, `--t-h2`, `--t-body` (13.5px), `--t-sm`, `--t-xs`, `--t-eyebrow`; shape `--radius` (6px), `--radius-sm`, `--border` (1px hairline), `--shadow-soft`. Density is application density: 32px table rows, 12–16px panel padding, 4px rhythm.

State is a channel, never a colour: pass the settled state text to `StatusChip` (`Not ready`, `Review`, `Held`, `Completed`, `Needs sorting`, `Blocked`, `Stale`…) and it picks amber/navy/red/green/neutral and an icon; pass `state="review" | "not-ready" | "held" | "pending" | "blocked" | "completed" | …` to `Record`, `Metric`, `QueueCard`, `Blocker` for the accent rail. Green is confirmed completion only. Red is spent on exactly four things: the one `PrimaryAction`/`Button variant="primary"` per screen, the active nav route, focus, urgent emphasis. Compact bar/row actions are `Button` (default hairline, `dark` for the committed action, `light` on the dark band); page-level form submits are `PrimaryAction`/`SecondaryAction`. A disabled action stays visible and states its condition (`Button disabled condition="Available in Review"`).

Copy is business language for operators: no Azure/OCR/queue/adapter/lease/deployment/AI-engine terms; never the word “intake” (use Inbox, Upload, Queues); a zero result renders `0` or an `EmptyState`; a genuine failure renders `StatusCard variant="error"` with the last-good time. Provenance is an icon with a one-word tooltip (`Provenance word="Extracted"`), never a sentence. Icons come only from `Icon` (16 Lucide glyphs: search, user, refresh-cw, clock, calendar, check-circle, alert-triangle, alert-circle, info, file-text, filter, shield, chevron-right, arrow-right, upload, lock).

## Where the truth lives

Read `styles.css` (tokens in `:root`, the `[data-state]` channel, and every class) and `guidelines/design.md` (the design authority: principles, tokens, per-surface rules) before styling anything. Each `components/<group>/<Name>/<Name>.prompt.md` carries that component's rules and examples.

## One idiomatic screen

```tsx
const { AppShell, AppNav, PageHeading, Refresh, MetricStrip, Metric, Panel, SectionLabel, QueueList, QueueListRow, StatusChip } = window.PegasusDS;

<AppShell nav={<AppNav userName="j.patel" items={[
  { label: 'Dashboard', href: '/', current: true }, { label: 'Inbox', href: '/Mail' }, { label: 'Upload', href: '/Upload' },
  { label: 'Queues', href: '/Triage' }, { label: 'Cases', href: '/Cases' }, { label: 'Operations', href: '/Operations' },
]} />}>
  <PageHeading title="Dashboard" refresh={<Refresh updatedAt="14 Aug 09:32" />} />
  <SectionLabel>Active cases</SectionLabel>
  <MetricStrip columns={3}>
    <Metric label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="/Triage?queue=not_ready" />
    <Metric label="Review" icon="info" state="review" value={12} href="/Triage?queue=review" />
    <Metric label="Held" icon="clock" state="held" value={3} href="/Triage?queue=held" />
  </MetricStrip>
  <SectionLabel>Case work due</SectionLabel>
  <QueueList>
    <QueueListRow href="/Cases/1" title="CE-2026-01432" subtitle="Awaiting repairer images"
      end={<><StatusChip state="Awaiting information" /><small>Next chase 18 Aug</small></>} />
  </QueueList>
</AppShell>
```

# PegasusDS (@pegasus/design-system@0.1.0)

This design system is the published @pegasus/design-system React library, bundled as a single
browser global. All 81 components are the real upstream code.

## Where things are

- `_ds_bundle.js` — the whole-DS bundle at the project root; loads every component to `window.PegasusDS`. First line is a `/* @ds-bundle: … */` metadata header.
- `styles.css` — the single stylesheet entry: it `@import`s the tokens, fonts, and component styles (`_ds_bundle.css`). Link this one file.
- `components/<group>/<Name>/<Name>.prompt.md` (example JSX + variants), `<Name>.d.ts` (types), `<Name>.html` (variant grid).
- `tokens/*.css` — CSS custom properties, names verbatim from upstream.
- `fonts/` — `@font-face` files + `fonts.css` (when the package ships fonts).
- `guidelines/` — the design system's own usage guidance (1 doc(s), see `guidelines/index.md`). Read these before composing larger layouts.

For a specific component, `read_file("components/<group>/<Name>/<Name>.prompt.md")`.

## Loading

Add these two lines to your page once (React must be on the page first):

```html
<link rel="stylesheet" href="styles.css">
<script src="_ds_bundle.js"></script>
```

Components are then available at `window.PegasusDS.*`. Mount into a dedicated child node (e.g. `<div id="ds-root">`), not the host page's own React root, so the two trees don't collide:

```jsx
const { AcceptanceBoundary } = window.PegasusDS;
ReactDOM.createRoot(document.getElementById('ds-root')).render(<AcceptanceBoundary />);
```

## Tokens

54 CSS custom properties from @pegasus/design-system. Names are
preserved verbatim from upstream. They are declared inside `_ds_bundle.css` (this DS ships one compiled stylesheet rather than separate token files).

- **radius** (2): `--radius`, `--radius-sm`
- **shadow** (2): `--shadow-soft`, `--shadow-modal`
- **other** (50): `--ce-red`, `--ce-red-dark`, `--ce-red-tint`, …

## Components

### status
- `AcceptanceBoundary` — .acceptance-boundary  an amber block naming what this surface does not yet prove.
- `Blocker` — .blocker  one unmet requirement naming its own field and resolution. Render inside BlockerList.
- `BlockerList` — .blocker-list  the readiness rail's list of Blockers.
- `EmptyState` — .empty-state  muted business-language copy for a zero result.
- `FailureDetail` — .failure-detail  the red-railed detail block under a failed action.
- `Notice` — .notice  an amber note above a form or list: one consequence the operator must understand.
- `Provenance` — .prov  where a value came from: a small icon whose tooltip, on hover and
- `StatusCard` — .status-card  a left-railed feedback card. Every state also carries text
- `StatusChip` — Pill-shaped state chip: text + Lucide icon on a tinted ground. Every chip
- `ValidationSummary` — .validation-summary-errors  the red-railed form error summary the tag helper emits.

### data
- `ActionList` — .action-list  a wrapping row of inline actions or facts.
- `DataTable` — .table-wrap  table  the operational table: 32px rows, uppercase
- `FilterBar` — .panel.filterbar  one line of common filters with the rarely used fields
- `Pager` — .pager  accessible Previous / context / Next pagination never infinite scroll.
- `PlainList` — .plain-list  a simple bulleted list with the 8px rhythm.
- `TableWrap` — .table-wrap  the bordered, horizontally scrolling wrapper for a hand-written table.

### metrics
- `AdminCard` — .admin-card  an administration workspace entry: icon square, linked title (whole card is the target), one-line description.
- `AdminWorkspaces` — .admin-workspaces  auto-fit grid (min 300px) of AdminCards.
- `Metric` — .metric  one compact tile: label with icon, count at the bottom, a 3px
- `MetricStrip` — .metric-strip  a single row of compact Metric tiles reflows to 4/2/1 columns on narrow viewports.
- `MetricTile` — .metric-tile  a bordered tile in a TileGrid: icon square, big count, label.
- `QueueCard` — .queue-card  a queue tile: optional icon square, label, big tabular
- `QueueFilters` — .queue-filters  a row of hairline filter links above a queue list.
- `QueueGrid` — .queue-grid  auto-fit grid of QueueCards (min 220px each).
- `QueueList` — .panel.queue-list  a panel whose rows (QueueListRow) are each one full-row target. Already a panel: do not nest it in Panel.
- `QueueListRow` — One row of a QueueList: identity left, state right, full-row link.
- `TileGrid` — .tile-grid  a two-column grid of MetricTiles that share hairline borders.

### shell
- `AppNav` — .app-nav  the white top bar: brand (logo + product name), primary
- `AppShell` — The authenticated page frame: skip link, AppNav, .app-shell  main,
- `BackLink` — .back-link  muted return link with the arrow rotated to point back.
- `Crumb` — .crumb  a one-line breadcrumb above a record: Cases / CE-2026-01432.
- `FreshnessBanner` — .freshness-banner  the full-width freshness strip (older screens new screens use Refresh).
- `PageHeading` — .page-heading  H1 (with optional eyebrow) and the screen's safe primary action or freshness element, above a hairline.
- `Refresh` — .refresh  the compact corner element: last-good time, a chip only when
- `SectionTabs` — .section-tabs  page-level section navigation (assessment sections), mirroring the shell's active-route underline.

### auth
- `AuthCard` — .auth-card  the single centred card family: sign in, signed out, password
- `AuthCardActions` — .auth-card__actions  stacked full-width actions inside an AuthCard.
- `AuthShell` — .auth-shell  the navless, centred, full-height paper ground for sign-in and status cards.
- `SupportReference` — .auth-card__reference  the support reference code with a Copy .btn use as AuthCard.foot content after Support reference.

### record
- `Blockhead` — .blockhead  a block header inside a record body: title left, controls right.
- `DataRow` — .datarow  one field/value line with provenance or an action at the end.
- `DetailList` — .detail-list  a two-column dl (10rem term column, bold values, hairline rows).
- `EvidenceFigure` — .evidence-figure  a read-only guide figure on the recessed ground (label, value, source stacked).
- `EvidenceList` — .evidence-list  a bulleted two-column list of evidence facts.
- `Facts` — .facts  compact fact columns inside a record body: titled dls, 28px rows, tabular numerals.
- `FieldCard` — .field-card  one extracted field: title, value, small detail conflict adds the amber rail.
- `FieldGrid` — .field-grid  hairline-separated grid of FieldCards (min 260px).
- `ProposalDiff` — .proposal-diff  recorded value beside proposed value, equal weight.
- `Record` — .record  THE RECORD CONTAINER. A screen about one record is one
- `RecordBar` — .record__bar  the sticky action bar: state actions left, the committed action right behind a hairline rule.
- `RecordBody` — .record__body  the container's content area (16/20px padding).
- `RecordHead` — .record__head  the dark header band: reference, identity facts, state chip then the stage accent.
- `Subtabs` — .subtabs  pill sub-navigation for a nested level (folder, sub-state) the current pill is filled charcoal.
- `Tabs` — .tabs  the record container's tab row on the paper ground: red underline

### actions
- `Button` — .btn  the compact action-bar button used in record bars, table rows and
- `ButtonRow` — .button-row  a wrapping flex row of actions with the 10px gap.
- `Gated` — .gated  wraps a disabled control so its unlocking condition shows as a one-line tooltip.
- `PrimaryAction` — .primary-action  the page-level form submit in Collision red. One per
- `SecondaryAction` — .secondary-action  the page-level hairline companion to PrimaryAction (Cancel, Back, alternative).
- `SendToClaudeButton` — .send-action  the one recorded divergence from the palette: the Engineer

### forms
- `Choice` — label.choice  a checkbox or radio with its text on one line, red accent colour.
- `ChoiceGroup` — fieldset.role-choices  a bordered group of Choices with a legend wraps in a row, or stacks.
- `Field` — A labelled control cell for FormGrid: label, control, hint, validation  the .form-grid  div shape.
- `FormGrid` — .form-grid  auto-fit field grid (min 240px per field) use Field wide to span.
- `FormPanel` — .panel.form-panel  the standard form section: title, then a form laid out as a 12px grid.
- `Input` — input  34px tall, hairline line-strong border, 5px radius readonly/disabled recess onto paper.
- `RoleForm` — .role-form  the narrow in-table administration form (choices, reason, save).
- `RowConfirm` — details + .row-confirm  an action that needs a reason confirms in
- `Select` — select  same treatment as Input.
- `Textarea` — textarea  5rem minimum, vertical resize.

### layout
- `DashboardGrid` — .dashboard-grid  two equal columns of panels (single column under 1280px).
- `Eyebrow` — .eyebrow  the small uppercase muted label above a heading or figure.
- `Icon` — Lucide line icon rendered inline with the .icon class: 2px stroke,
- `Lede` — .lede  a muted one-line intro. Design rule: screens carry no lede use only beside a consequential control.
- `Panel` — .panel  a white card on the paper ground: 16px padding, hairline border, 6px radius, soft shadow.
- `ReviewGrid` — .review-grid  two equal columns for side-by-side review.
- `SectionLabel` — .section-label  an eyebrow-styled h2 that names a section of a panel.
- `SplitMain` — .split-main  the list leads (2fr), the form that adds to it follows (min 300px).
- `SrOnly` — .sr-only  visually hidden text for assistive technology.
- `WorkbenchGrid` — .workbench-grid  the Engineers assessment workbench: sticky rail + main column.

### overlay
- `ReasonDialog` — .reason-dialog-backdrop  .reason-dialog  the modal that collects a
