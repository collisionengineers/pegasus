# Razor Pages UI review guide

Use only the sections relevant to the change.

## Page and workflow

- Can the intended user identify the page, current state, next action, and consequence?
- Does the reading and focus order match the task order?
- Are important actions distinct from navigation and secondary actions?
- Are destructive or irreversible consequences clear at the decision point?
- Does returning, validation failure, or a conflict preserve valid work and context?

## Forms and feedback

- Are controls semantic, labelled, grouped where useful, and supplied with relevant instructions?
- Are server-side validation errors specific and associated with the affected inputs?
- Is the summary useful without duplicating or obscuring field errors?
- Do status messages reach assistive technology when content changes dynamically?
- Are disabled controls understandable, or would explanatory adjacent content or a different flow work better?

## Tables, components, and overlays

- Use data tables only for genuinely tabular relationships, with correct headers.
- Confirm component variants and states fit their content, including long or missing values.
- Verify menus, tabs, dialogs, tooltips, and disclosures with keyboard and pointer input.
- For dialogs, check initial focus, containment where appropriate, Escape/cancel behavior, and focus return.
- Check that hover-only information is also available on focus and does not cover essential content.

## Responsive and accessible presentation

- Test supported widths and 200% zoom rather than inferring behavior from CSS alone.
- Check logical reflow, readable line lengths, overflow, sticky regions, and focus visibility.
- Verify meaning is not carried by colour, position, icon, or animation alone.
- Check reduced-motion and forced-colour behavior when the interface uses motion or custom colours.

## Libraries and assets

- Confirm the library is already approved or that adding it is within scope.
- Inspect only the components and configuration actually used; library-level claims do not prove the page.
- Check duplicate CSS/JS, blocking assets, initialization failures, unused bulk, version pinning, and production asset paths.
- Prefer measured performance evidence when payload or responsiveness is a concern.

## Mockup rounds

- Does each panel head carry one primary and one menu, with any exception named in the notes?
- Does read mode show values only: no empty inputs, no unapplied presets, no permanent drop zone?
- Is availability a label per section, never a disabled control with no stated reason?
- Does entering or leaving edit move the page or reflow the grid?
- Is any fact shown more than once across ribbon, aside and body?
- Has explanatory copy appeared on the page? Labels and captions only.
- Does the coverage audit list every live control and fact, and are the drops restored or recorded as deliberate?
- Do labels match the live `.cshtml` strings and `OperatorLabels`, with CONTEXT.md overriding where they conflict?
- Is every FRD-changing choice a lettered sign-off item, and is every settled item dated?
- Does the self-check report zero fails and no console errors, and are the three-width screenshots present for every state the notes cite?

## Stage 2 conformance

- Same state, same width: the routed page beside the numbered mockup shot.
- Each numbered rule in `how-it-should-work.md` proven on the routed page.
- The mockup strip, query presets, strip variables and synthetic fixtures have not shipped.
- The "Documentation impact" edits landed with the code.

## Sources

- [Microsoft: Razor Pages](https://learn.microsoft.com/aspnet/core/razor-pages/?view=aspnetcore-10.0)
- [Microsoft: ASP.NET Core integration tests](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [W3C: WCAG 2 at a glance](https://www.w3.org/WAI/standards-guidelines/wcag/glance/)
- [W3C: Keyboard interface guidance](https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/)
- [W3C: Forms tutorial](https://www.w3.org/WAI/tutorials/forms/)
- [W3C: Tables tutorial](https://www.w3.org/WAI/tutorials/tables/)
