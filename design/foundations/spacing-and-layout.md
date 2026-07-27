# Spacing and layout

## Layout model

Use the upstream 4px base rhythm, 2px corners, 1px hairline borders, rare soft
shadows, and 24px primary gutters. The current proof uses a centered shell,
grid-based queue/dashboard/review panels, and practical 44px action targets.
Planned V1 uses dense desktop multi-pane work at 1280px and wider; the upstream
marketing 1200px/96px section rhythm is not imported.

## Responsive behavior

At constrained desktop widths and 200% zoom, essential content reorders into
labelled sections/tabs/drawers without losing identity, labels, focus, or
actions. Mobile staff UI is `Not planned`; CSS reflow does not create a mobile
product.

## Canonical source and runtime

Planned rules: `design/product/requirements.md`. Exercised layout/breakpoints:
`src/CollisionSpike.Web/wwwroot/css/site.css`.
