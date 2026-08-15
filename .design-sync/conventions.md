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
