# UI alpha design pass

Design-only visual and conformance pass over the Operations-first
`0.1.0-alpha.1` shell. Entirely view layer: `.cshtml`, `site.css`, and the
`AccessibilityTests` route list. No Core, Infrastructure, or Worker change,
and no change to PageModel query or command logic.

## Scope

Capability IDs: UI-01, UI-02, UI-03, UI-04, UI-05, UI-06, UI-08, UI-09,
UI-11, UI-13 ([capabilities.md](../capabilities.md)).

Widened on 2026-08-03 by operator decision to also carry the **design route
for UI-15** (Engineer assessment workbench, `Later / 1.0.0`) and its
**`Send to Claude` action surface** (AI-09, `Later / 1.3.0`). The claim line
on `origin/dev` records the same widening.

That addition is design and markup only. `design/README.md` § Deferred
casework allows no alpha route, control or placeholder for an Engineer
workbench or for `Send to AI` work, and requires every deferred UI capability
to re-enter specification and review before implementation. This is that
artifact: the surface is built **unlinked** — absent from `_Layout`
navigation and from the Case workspace — and activates no route, Core field
or transport. It must not be linked without that approval.

Excluded and why:

- UI-07 (search/filter) — in flight under `task/image-led-intake`.
- UI-10 (email-management workspace) — allocated `Next / 0.3.0`, not
  `0.1.0-alpha.1`.
- UI-12 (responsive/mobile) — `Not planned`, permanent boundary.

Also excluded as non-alpha sub-surfaces: report-image selection (future
Engineers screen), the image-readiness advisory (AI-05, `Later / 1.0.0`),
email quick preview and mailbox-refresh mechanics (belong to UI-10), and
any upload limit beyond the interim bound in
[open-decisions](../open-decisions.md).

## Evidence state

**Implemented.** Not caller-proved beyond the authenticated Development
route, not deployed, not operator-accepted. No accessibility *acceptance*
is claimed; automated scanning and manual desktop review are recorded
below, and the remaining review items in
[design/README.md](../../design/README.md) § Accessibility are outstanding.

## What changed

**Defects fixed (present before this task).**

- `_Layout.cshtml` never rendered `_LucideSprite.cshtml`, so every
  `<use href="#icon-…">` in the app resolved to nothing. The sprite is now
  rendered once per page.
- The sprite defined glyphs as `<g id>` while every consumer used
  `<svg class="icon">` with no `viewBox`, so icons would have rendered
  clipped even once present. Glyphs are now `<symbol viewBox="0 0 24 24">`,
  and `.icon` carries the approved 2px stroke and round caps, which `<use>`
  clones do not inherit from the sprite root.
- `_StatusChip.cshtml` appended `" (0)"` to any state without a digit, so
  `Review` displayed as `Review (0)`.
- `_MetricCard` referenced `queue-card--green` and `_FreshnessBanner`
  referenced `freshness-banner--loading`; neither class existed.
- CSS targeted `.validation-summary`/`.field-validation`, but the tag
  helpers emit `validation-summary-errors`/`field-validation-error`, so
  validation styling never applied to generated markup.
- `_FreshnessBanner` stamped a UTC value with the label "London", an hour
  wrong under BST. It now converts to Europe/London, the day boundary the
  dashboard counts against.

**Token layer.** `site.css` rewritten around the approved sets: full colour
palette including the amber and navy triads that were previously inline
literals; the `4/8/12/14/18/24/32/40/64` spacing rhythm; a restrained type
scale (`h1` from ~50px to 24px, body 15px, tabular numerals on metric and
numeric values); 2px radius throughout, replacing the 3px divergence
`design/README.md` already flagged. Every unapproved one-off literal
(`#485568`, `#6d5321`, `#9b9995`, `#78808c`, `#667085`, `#dda01f` and the
rest) now resolves to an approved token — **no new colour token was needed
and no reviewed divergence is claimed**. Navigation moved from cool slate
to warm charcoal, and the dead `.brand-mark` rule was removed.

**State channel.** One `--state-line`/`--state-bg`/`--state-fg` channel,
selected by `data-state` or a tone class, drives card rails, row rails,
chips and icon tints. `_StatusChip` is the single place a business or query
state chooses its tone and glyph, now covering the full state contract
rather than four states. Every chip carries its text label, so no state is
conveyed by colour or icon alone. Where a tile carries both what it is and
whether its datum loaded, absence wins: an unavailable tile does not borrow
the urgency of the queue it stands for.

**Operations dashboard.** Rebuilt to the shape `design/README.md` §
Operations-first shell already specifies and the code had never
implemented: a seven-across metric strip, a second tier for today/this
week, freshness and manual refresh, then a split pane. Fake single-letter
glyphs (`E`, `R`, `!`, `T`) and the text chevron were replaced with mapped
Lucide icons. An absent query now renders a quiet `Unavailable` chip
instead of the word set at metric size, which previously made a missing
query the loudest element on the page.

**Administration and Account.** All twelve Administration views and the
three Account views used bare `<h1>`/`<input>`/`<button>` and default
browser-blue underlined links. They now use the application's own
vocabulary: `_PageHeader`, `.panel`, `.status-card`, `.primary-action`,
`.table-wrap`, `scope="col"` on every column header, `<caption>` on every
table, and `_StatusChip` in place of raw enum text.

**Refresh feedback.** `design/README.md` requires manual refresh to give
start feedback. The Refresh control now sets a busy state, disables to
prevent double submission, and changes its label to `Refreshing`. A spin on
the icon decorates that; the label carries the meaning, so reduced motion
loses nothing. This is the only animation in the product and no duration or
easing token was introduced.

**UI-15 assessment surface.** Two routes under
`src/Pegasus.Web/Pages/Cases/Assessment/`: the workbench, with seven freely
reachable sections chosen by `?section=`, and the returned-suggestions review.
Both PageModels are `[Authorize]` plus an empty `OnGet`.

The arrangement follows the report's required sections rather than EVA's
twelve-step wizard, which `capabilities.md` UI-15 excludes copying. A case
header and a permanent readiness rail sit beside the section, so what is still
outstanding is never a step the operator has to walk to. The estimate is
entered line by line — type, code, description, work units, price, part
number, betterment, status and evidence label — with the category totals shown
as derived and the arithmetic printed beside each row; the report's three name
lists are grouped from those lines rather than typed. Rates come from a chosen
rate card displayed with its own dates and caveat: no rate figure is written
into the page, because `AGENTS.md` holds that a protected external skill
package never becomes an application policy owner. Valuation keeps guide
figures as read-only evidence, visually separate from the three figures that
reach the report. Every field renders empty — no case, claimant, registration,
staff name or rate figure is invented anywhere in the markup.

**`Send to Claude`.** Recorded in [design/README.md](../../design/README.md)
§ Tokens as a reviewed divergence authorised by the operator and scoped to the
single `.send-action` control. Every value is a local custom property on that
control rather than an addition to `:root`: the gradient and the 12px radius
each appear once in the stylesheet, the other 23 radius rules still resolve to
the approved 2px, the icon sprite is unchanged at 16 glyphs, and no font file,
bundle or external stylesheet is added. Reduced motion removes the lift, the
sparkle animation and the ember canvas, which the script then declines to
start; forced colours drops the gradient and restores a `ButtonText` border.
The divergence record states the one thing it does not solve — white on that
gradient measures about 2.3:1, 3.0:1 and 4.2:1 across the three stops against
the 4.5:1 this size and weight require. The gradient is the provider's own and
was adopted as given; the remedy is an operator decision.

**`_ReasonDialog` defects fixed.** It referenced an undefined `var(--red)`, so
the required-field marker took an inherited colour, and it had no initial
focus, focus containment, focus return or Escape handling — all four required
by `design/README.md` § Component map.

## Deliberate deviations from the approved plan

- **Typed partial models were not added.** The plan proposed converting the
  shared partials from `ViewData` to typed records. That means new C# types
  in a task scoped design-only; a normalised string mapping in
  `_StatusChip` gives nearly the same safety with no non-view footprint.
- **`_ReasonDialog` was not wired.** It remains built-but-unused. Wiring it
  would replace the inline reason inputs on roughly fifteen mutation forms,
  which changes how permanent actions are confirmed — behaviour, not
  design, and too large to carry safely here. It stays a live finding
  against `engineering.md`'s built-but-unwired rule.
- **`_LeasePanel` was not extracted.** The duplicated lease block in
  `Operations/Requests.cshtml` is unchanged; extraction is refactoring
  without visual benefit and the page was not otherwise restructured.
- **Not-found panels were reverted.** They were written, then removed on
  finding that all four handlers return `NotFound()`, making the branches
  unreachable. Building a styled surface for an unreachable state is the
  dead-code this repository forbids; 404 is owned by the error page.

## Verification

- `dotnet build --configuration Release` green.
- Tests green with zero failures across all three projects:
  `Pegasus.Core.Tests` 186, `Pegasus.ArchitectureTests` 73, and all 343
  `Pegasus.IntegrationTests`. The integration project was run in four
  filtered chunks because a single run takes ~28 minutes; coverage was then
  proved rather than assumed by enumerating `--list-tests` per chunk and
  confirming the union equals the full discovered set in both directions.
  One test (`InstructionDraftWebTests.SameManualUploadTokenWithDifferentBytes…`)
  matches two chunk filters and therefore ran twice; no test ran zero times.
- `AccessibilityTests` extended from 6 authenticated routes to 19, adding
  every Administration sub-route, Search, both Operations routes,
  `/Account/AccessDenied`, and `/ImageIntake` (introduced by `origin/dev`
  during this task and therefore inside this styling). All 26 browser tests
  pass after the merge: zero axe violations, exactly one `<main>` and one
  `<h1>` per route, no horizontal overflow at 1024×768 or 512×768,
  forced-colours and reduced-motion renders clean, and queue state not
  conveyed by colour alone.
- The two assessment routes are **not** in that theory: they take a case id,
  and no seeded case exists in the browser fixture. They were verified by
  hand instead — both return 200, one `<h1>` and one `<main>` each, all
  controls labelled, and the reduced-motion guard confirmed live (the canvas
  never attaches and the sparkle animation resolves to `none`).
- Manual review at 1280, 1024 and 512 against the running Development
  caller, with before/after captures of Operations, Administration, Cases
  and Staff accounts.

Not verified: populated Case, Intake and Triage detail states, which need
case data this pass did not create (fabricating operational material is
forbidden). Those views received consistency edits only — status banners
and state chips — and were checked in their empty state. `/Account/SignIn`
is not covered by the axe theory because the DevelopmentOffline profile
authenticates automatically and redirects it; it shares the `.auth-panel`
markup with `/Account/PasswordChange`, which is covered.

## Open question for the operator

The Operations dashboard labels the `DraftReady` intake count **Review**.
`design/README.md` § "Core outcome to operator label" maps `DraftReady` to
the exact operator label **Instruction draft** and says implementations
must not invent aliases; `Review` is a Case state, and this count is of
pre-case intake drafts. The existing wording was left unchanged because
changing it changes what an operator reads. It needs a business decision,
not a design one.

## Review addendum (2026-08-03, PR #326 independent review)

- The two Assessment surfaces no longer register routes: `@page` directives
  and the two empty PageModels are deleted, so "activates no route" in the
  operator widening is now literally true. design/README.md § Deferred
  casework records the authorized routeless artifact. Restoring the routes
  belongs to the wiring task's re-entry approval.
- The saved-browser reference files (`sendtoclaudeexample.html` and its
  `_files` css) are deleted; the design/README.md divergence table is the
  record of the `Send to Claude` values.
- After merging dev (PR #327), the Automation administration surface was
  conformed: Administration index entry is an `.admin-card`, both Automation
  pages use `_PageHeader`/`.back-link`/`_StatusChip`, and both routes joined
  the accessibility theory.
