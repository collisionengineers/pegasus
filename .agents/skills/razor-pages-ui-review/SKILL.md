---
name: razor-pages-ui-review
description: Review server-rendered ASP.NET Core Razor Pages and MVC view designs or implementations for usability, accessibility, responsive behavior, component consistency, Razor fit, and frontend library cost. Use for UI audits, design reviews, or .cshtml/CSS/JavaScript diffs; not for Blazor-only reviews.
---

# Razor Pages UI review

Review the interface against its stated users, tasks, requirements, repository conventions, and actual implementation. Do not substitute a personal aesthetic or redesign unrelated areas.

## Establish the review boundary

Read the governing product and design material, then inspect the affected page, page model, shared components, styles, scripts, tests, and relevant library documentation. Distinguish requirements from recommendations and implementation defects from optional refinements.

## Review lenses

- **Task and content:** purpose, hierarchy, terminology, action prominence, and unnecessary effort.
- **States and recovery:** loading, empty, validation, success, conflict, partial, stale, unavailable, failure, and preservation of user input.
- **Accessibility:** semantic structure, names and labels, keyboard operation, focus order and visibility, announcements, contrast, zoom/reflow, target usability, forced colours, and reduced motion.
- **Responsive behavior:** preserved content and actions, sensible reflow, long content, tables, overlays, and supported viewport/zoom combinations.
- **Razor fit:** correct use of layouts, partials, View Components, Tag Helpers, binding, validation, antiforgery, and server ownership of business rules.
- **Components and libraries:** consistent use, appropriate variants, real contextual accessibility, styling conflicts, duplicated capabilities, JavaScript requirements, payload, maintenance, licensing, and upgrade cost.
- **Evidence:** tests and runtime checks that prove behavior rather than merely matching markup text.

Read [references/review-guide.md](references/review-guide.md) for concrete checks when the surface is substantial.

## Report findings

Lead with findings ordered by user impact and confidence. For each material finding, identify the affected location or behavior, why it matters, and a proportionate remedy. Offer alternatives where more than one design is reasonable. Separate questions and optional improvements from defects.

If no material findings exist, say so and name any important validation gap. Do not manufacture findings to fill a report.
