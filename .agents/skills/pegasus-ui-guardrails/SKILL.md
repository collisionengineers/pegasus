---
name: pegasus-ui-guardrails
description: Mandatory Pegasus Web visual/design guardrails. Read before changing any file under src/Pegasus.Web, including Razor, shared layouts, CSS, JavaScript, presentation labels, dialogs, images, or page structure.
---

# Pegasus UI guardrails

These rules apply to every change under `src/Pegasus.Web`.

They exist to prevent incidental feature work, bug fixes, refactors, or agent preference
from redesigning Pegasus. They supplement the repository-root instructions, the Razor
UI skills, `docs/design/README.md`, and FRD-12.

## Authority

Use this order when requirements conflict:

1. The operator's explicit instruction for the current task.
2. Current functional requirements, especially `docs/frd/frd-12-operator-experience.md` (shell and page contract), `docs/frd/frd-15-work-centre-queues-and-search.md`, `docs/frd/frd-16-case-record-workspace.md` and `docs/frd/frd-17-administration-workspace.md`.
3. `docs/design/README.md` for presentation, components, assets, layout and visual language.
4. The current routed implementation and shared design-system classes.
5. Historical mockups/commits only as evidence of intent.

A feature request, bug fix, backend change, or refactor is **not** permission to redesign
the UI. Unless the task explicitly asks to change layout/style/design, preserve the
existing v26 visual contract and place the new behaviour inside the established pattern.

If an explicit task does require a lasting design-contract change, update the owning
design/FRD documentation in the same change. Do not silently create a second convention.

## Mandatory pre-edit check

Before changing Web UI:

- Read the relevant portion of `docs/design/README.md`.
- Read the relevant interaction requirement in FRD-12.
- Inspect the nearest current routed page/partial and the shared CSS/JS it already uses.
- Reuse the existing component/class/interaction when the same need already exists.
- State internally whether the task explicitly authorizes a design/layout divergence.
  If it does not, the design is frozen except for the smallest local correction needed.

For routed Razor work also follow the existing `razor-pages-ui-design`,
`razor-pages-ui-implementation`, and `razor-pages-ui-review` skills as applicable.

# Product character — do not reinterpret

Pegasus is a restrained, desktop-first, internal operational case-management application.
It is not:

- a marketing site;
- a consumer dashboard;
- a mobile-first product;
- an analytics showcase;
- a card-heavy SaaS admin template;
- an experimental design surface.

Optimize for scanning, comparison, decision-making, evidence review, and repeated office
use. Prefer compact, stable geometry over decorative whitespace or visual novelty.

## Approved visual baseline

Preserve the current design-system values unless the task explicitly changes the design:

- Inter Variable, self-hosted.
- Body text approximately 13.5px.
- Standard controls: 36px.
- Dense operational rows: 40px.
- Rail: 220px expanded, 64px collapsed.
- Utility bar: 48px.
- No working-set strip: it was removed from every page (operator, 24 September 2026).
- Main page padding: 18px desktop.
- Content cap: 1580px and centred.
- Base radius: 3px; large radius: 4px.
- Dark warm-charcoal navigation/shell.
- White and light-neutral work surfaces.
- Near-black text.
- Collision red used sparsely for brand/primary emphasis and destructive separation.
- Amber = incomplete/pending/held; navy = Review; blue = active/running/With Engineer;
  green = completed/confirmed; red = failure/correction; neutral = closed/cancelled/unknown.

Do not introduce a new palette, larger radius system, glassmorphism, decorative gradients,
large shadows, marketing illustrations, oversized typography, or a new spacing scale.

## Layout guardrails

- Use the established shell, page hierarchy, panels, panes, tables, ribbons, rows and dialogs.
- Do not replace a dense table with cards when rows represent the same comparable fields.
- Do not "cardify" every section.
- Keep bounded forms aligned. Do not stretch controls merely because width is available.
- Use wide desktop space for useful panes, evidence, comparison or tables, not empty decoration.
- Reflow before hiding. Supported narrower widths must retain information and actions.
- Do not change rail order, shell geometry, content width or breakpoints as a side effect of a
  page feature. Do not reintroduce a working-set strip or any other strip of record tabs.
- Do not create a second shell, second page-header convention, or second record-frame convention.
- A page-local overflow problem must be fixed locally. Do not broaden a global selector to solve
  one component's sizing problem.

## One fact, one home

Avoid duplication. A business fact, evidence set, or primary action should have one obvious
home.

- Do not repeat the Case image gallery in another section merely for convenience.
- Do not repeat status in a heading, pill, banner and disabled control.
- Do not expose two routes that perform the same business action.
- A secondary location may show a concise summary or link to the owning surface, not another
  editable copy of the same concept.

Recent examples that are now guardrails:

- Case images belong under **Files → Images**; Damage does not repeat the image strip.
- Guide valuations have one route per source: the source's own card contains its
  **Get valuation**, which fills the card's boxes in place; the boxes belong to the Case form,
  so the ribbon **Save** records the card. Do not give a card its own Save, and do not
  reintroduce **Add valuation** or a second source-button row.
- The Estimate header does not show a locked pill saying a confirmed Engineer's Value is
  required when the action is already absent until the condition is met.

## Actions and control placement

- Keep one visually clear primary action for the current context.
- Secondary actions belong in the existing secondary/action-menu pattern.
- Destructive actions use the existing danger treatment and separation.
- Do not scatter equivalent actions across the ribbon, panel head, body and footer.
- Do not add a new button because an existing action is not immediately visible; first place it
  in the established action location for that surface.
- Toolbar controls keep natural width. Full-width select/input rules apply only in form/table
  contexts that own that geometry.

## Availability, blocking and status

Never use mystery-disabled controls.

Use the established pattern:

- render the action when it is available;
- omit it when omission is the established contract; or
- use the established `.gated`/availability treatment when the operator must understand why
  the section is unavailable.

Do not add a warning/status pill that only restates the absence of an action or text already
visible next to it.

Every status chip carries a text label; colour is not the only cue.

Do not represent unavailable/loading/failed data as zero, blank, "none", or a success state.

## Reasons, confirmations and dialogs

Do not add "reason" boxes or confirmation dialogs by habit.

- A reason field/dialog exists only where Core/FRD/business policy requires a reason.
- Normal Case data Save does not gain a free-text reason merely for audit narration.
- **Cancel** means discard/cancel the current edit and must not ask a redundant confirmation.
- Navigation or section switching may still protect genuinely unsaved work.
- Use confirmation for irreversible/destructive or materially consequential actions where the
  current contract requires it.
- Account hard delete is such an exception.
- Dialogs are contextual and focused; do not turn routine actions into modal workflows.

Use the existing dialog root/focus-trap/inert/Escape/focus-return conventions.

## Language

Use the existing presentation-label owners (`OperatorLabels`, Case workspace labels, etc.)
rather than ad-hoc strings where a label owner exists.

Do not rename settled business language casually. In particular, keep the distinctions among
Case, Audit, Triage, Unidentified, Image Intake, Not ready, Review, Held, With Engineer,
Query and Complete.

Use operator-facing provider/product language already approved by the design authority.
Do not leak implementation/vendor-development wording into the UI.

## Icons and imagery

- Lucide glyphs name inline actions/states. One glyph means one thing everywhere.
- The refined Pegasus mark is the only brand imagery; it stays decorative beside text that names the product.
- Do not invent substitute marks, generated brand art, emoji icons, or a second icon family.
- Do not use a decorative mark as an action or state.
- Do not remove or replace the Pegasus logo unless explicitly instructed.

## CSS guardrails

`src/Pegasus.Web/wwwroot/css/site.css` is the shared design-system contract.

- Reuse existing tokens and classes before adding new ones.
- Product colours must come from approved tokens/patterns.
- Do not add inline `style` attributes or `<style>` blocks to Razor pages.
- Do not add an external font, CSS framework, icon CDN, or design library.
- Do not add page-specific raw colours to Razor markup.
- Do not alter a global control selector to fix one local component.
- New page/component CSS must be scoped to that component.
- Use existing size/spacing/radius tokens where they express the need.
- Pills/circles may use pill geometry; ordinary panels/cards/controls stay on the restrained
  radius system.
- Do not create a second button, chip, panel, field, dialog or table vocabulary when an existing
  class serves the same semantic purpose.
- Preserve forced-colour/high-contrast and reduced-motion behaviour.

If a shared token or base selector truly must change, treat it as a design-system change:
inspect every affected surface and verify shared-layout screenshots.

## Motion

Pegasus has no decorative motion system.

- Normal hover/focus transitions remain approximately 140ms.
- Dialog/toast entrance may use the established restrained opacity/translate step.
- Respect reduced motion.
- No hover scaling, CTA lift, staggered entrances, scroll reveals, parallax or decorative motion.
- Do not invent new easing/duration tokens without explicit design authorization.

## JavaScript guardrails

JavaScript enhances established server-rendered controls; it does not create a new visual
language.

- Reuse existing `data-menu`, dialog, toast, dirty-state and viewer conventions.
- Escape/outside-click behaviour must match sibling components.
- Do not add a custom menu/dropdown interaction when the existing frame convention fits.
- Preserve keyboard operation and focus restoration.
- Do not make a business action exist only in visual JavaScript when the established surface
  provides a server-capable route.
- Keep state names and DOM hooks semantic rather than styling-specific.

# Case workspace

Any change under `Pages/Cases` must also follow the Case-workspace rules in
[`references/case-workspace.md`](references/case-workspace.md); read it before editing a Case
page, partial, script or style. Do not infer Case layout from a generic page pattern.

# Explicit design-change gate

The following require explicit operator instruction to redesign/change UI, not merely a feature
request that touches the area:

- shell route order or rail structure;
- design tokens, font, palette, radii or global density;
- utility bar model, or bringing back a working-set strip;
- Case ribbon, section-row or section ordering;
- Scroll/Tabs default behaviour;
- Views card, Figures and Next action aside model;
- established ownership of a fact/action between Case sections;
- table-to-card or card-to-table presentation family;
- action hierarchy/placement;
- adding a new persistent page family or navigation destination;
- replacing the icon/mark system;
- changing responsive breakpoints.

When explicitly authorized, change the governing design document and affected tests in the same
work. Do not preserve the old contract in parallel unless requested.

# Verification

A Web UI change is not complete because the markup compiles.

For the affected surface, verify the relevant states:

- read/default;
- edit, if editable;
- empty/no-data;
- loading/pending when represented;
- failure/unavailable;
- held/locked/colleague editing where relevant;
- validation/conflict where relevant;
- destructive confirmation where relevant.

Visual verification:

- routed page changes: compare at 1580×1000 and a smaller desktop width;
- shared shell/layout changes: also verify at 760px;
- Case changes: verify read and edit geometry, and the affected section in Scroll/Tabs as relevant;
- confirm no horizontal spill, clipped toolbar, overlapping viewer controls, duplicate facts/actions,
  or unexplained whitespace.

Do not update a screenshot/reference solely to make a regression pass. A changed baseline requires
the same explicit design authorization as the underlying design change.

Run the focused Web/interaction tests that own the changed contract and any shared-shell tests
affected by shared CSS/JS. Report any browser walk still outstanding.
