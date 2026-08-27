---
name: razor-pages-ui-implementation
description: Implement or refactor server-rendered ASP.NET Core Razor Pages and MVC view UI using suitable layouts, partials, View Components, Tag Helpers, forms, CSS, JavaScript, Razor Class Libraries, or frontend libraries. Use for .cshtml UI work and component or library integration; not for Blazor-only component work.
---

# Razor Pages UI implementation

Translate an agreed experience into clear, maintainable Razor UI. Follow the repository's existing architecture, design system, dependency choices, and test conventions.

## Inspect before choosing a mechanism

Find the relevant page model, markup, shared views, styles, scripts, validation, tests, and asset pipeline. Search for an existing component or convention that already solves the need. Do not introduce a library, build tool, abstraction, or parallel styling system merely because it is available.

## Choose the smallest suitable Razor mechanism

- Keep page-specific composition in the page and its `PageModel`.
- Use a layout and sections for shared document structure and page-provided slots.
- Use a partial for reusable markup whose caller prepares the data.
- Use a View Component when a reusable UI region owns non-trivial rendering logic or obtains its own data.
- Use a Tag Helper when reusable server behavior naturally attaches to HTML-like elements or attributes.
- Use a Razor Class Library only when UI and assets genuinely need reuse across projects or packages.
- Use small local CSS or JavaScript when the behavior is local and the project has no suitable shared solution.

These are decision aids, not quotas. Avoid extracting a one-off fragment unless doing so improves clarity or matches an established convention.

## Preserve web and Razor behavior

- Prefer semantic HTML and native controls before recreating behavior with ARIA and JavaScript.
- Use Razor form, input, label, validation, and antiforgery conventions. Server validation remains authoritative even when client validation improves feedback.
- Preserve entered values and give specific, associated errors on invalid submissions.
- Keep links for navigation and buttons for actions.
- Make interactive behavior work by keyboard and expose meaningful names, states, and focus behavior.
- Implement required loading, empty, success, conflict, unavailable, and failure states without claiming more than the server knows.
- Keep JavaScript progressive and focused; do not move business rules into the browser.
- Follow the project's static-asset, caching, compression, and bundling approach rather than creating a second pipeline.

Read [references/razor-patterns.md](references/razor-patterns.md) when choosing among Razor mechanisms. Read [references/components-and-libraries.md](references/components-and-libraries.md) when selecting, adding, or integrating reusable components or external UI libraries.

## Verify proportionately

Run the repository's focused tests and build checks. Exercise the changed journey with realistic states and validate generated HTML, form behavior, keyboard use, responsive layout, and asset loading where relevant. Do not treat a successful build as proof of usable UI.
