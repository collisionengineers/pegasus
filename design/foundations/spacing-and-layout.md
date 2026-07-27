# Spacing and layout

## Layout model

The current proof uses a centered maximum-width application shell, grid-based
queue/dashboard/review panels, border-first grouping, and practical 44px action
targets. Planned V1 uses dense desktop multi-pane work at 1280px and wider.

## Responsive behavior

At constrained desktop widths and 200% zoom, essential content reorders into
labelled sections/tabs/drawers without losing identity, labels, focus, or
actions. Mobile staff UI is `Not planned`; CSS reflow does not create a mobile
product.

## Canonical source and runtime

Planned rules: `docs/plans/ui-ux/requirements.md`. Exercised layout/breakpoints:
`src/CollisionSpike.Web/wwwroot/css/site.css`.
