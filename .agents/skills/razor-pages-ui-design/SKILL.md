---
name: razor-pages-ui-design
description: Design or refine server-rendered ASP.NET Core Razor Pages and MVC view experiences. Use for page structure, workflows, forms, tables, responsive behavior, interaction states, visual hierarchy, or choosing suitable UI components and patterns; not for Blazor-only component work.
---

# Razor Pages UI design

Help shape a clear interface before or alongside implementation. Treat the user's goal and the project's design authority as primary; this skill supplies judgment, not a house style.

## Start with the actual product

Inspect the relevant pages, shared UI, styles, scripts, screenshots or designs, and governing documentation. Establish:

- who uses the interface and what they are trying to complete;
- the important information, decisions, actions, and consequences;
- existing layout, component, vocabulary, and responsive conventions;
- required states such as initial, loading, empty, validation, success, partial, stale, unavailable, and failure;
- explicit accessibility, browser, device, branding, and performance constraints.

Do not invent personas, product requirements, breakpoints, or a new design system to fill gaps. State a small assumption when one is needed.

## Design the experience

- Organize the page around the user's task and decision order, not the data model.
- Make primary information and actions easy to find; keep secondary detail available without competing for attention.
- Use familiar controls and semantic structures. Add custom interaction only when it materially improves the task.
- Preserve information and actions across supported widths and zoom. Reflow or reorder before hiding.
- Define feedback and recovery for realistic waits, invalid input, conflicts, failures, and destructive actions.
- Consider keyboard, screen-reader, pointer, touch, reduced-motion, forced-colour, and contrast needs as part of the design rather than a later decoration pass.

When several approaches fit, present two or three meaningful options with their tradeoffs and recommend the best fit for this context. Do not present a preference as a universal rule.

## Components and patterns

Reuse an existing, tested project component when it serves the same need. Adapt it when the context differs. Propose a new local component or external library only when the current parts cannot meet the requirement proportionately.

For deeper design criteria and component guidance, read [references/ux-and-components.md](references/ux-and-components.md).

## Deliverable

Provide enough for implementation: intended flow, page regions, component choices, important states, responsive behavior, accessibility considerations, and unresolved product decisions. Match detail to the size of the change.
