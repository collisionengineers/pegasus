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

## Sources

- [Microsoft: Razor Pages](https://learn.microsoft.com/aspnet/core/razor-pages/?view=aspnetcore-10.0)
- [Microsoft: ASP.NET Core integration tests](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [W3C: WCAG 2 at a glance](https://www.w3.org/WAI/standards-guidelines/wcag/glance/)
- [W3C: Keyboard interface guidance](https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/)
- [W3C: Forms tutorial](https://www.w3.org/WAI/tutorials/forms/)
- [W3C: Tables tutorial](https://www.w3.org/WAI/tutorials/tables/)
