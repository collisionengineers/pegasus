# 0005 — Reuse the brand's CSS design system

## Status

Accepted

## Context

Collision Engineers Ltd already owns a defined visual identity, captured as a CSS-native
design system (`collision-engineers-design`) and exercised by the prior `report-renderer`.
The look is precise and load-bearing: Documents red `#C80A32` for section-heading rules,
table header rows, the value figure and the red-bordered summary table; warm charcoal
`#2C2A27` text; grey label cells `#F2F2F2`; zebra striping `#F5F5F5`; `#BEBEBE` grid lines;
Arial/Helvetica body on A4. These documents go to courts, solicitors and insurers, so they
must match the house style exactly. The question is whether to re-express that look in a
layout API or to use the design system's own CSS directly.

## Decision

**Reuse the brand's CSS design system as-is** rather than re-implementing the look in a
layout API. The canonical stylesheet is the Pegasus-root
`design/assets/report-renderer/templates/report.css`, linked and embedded in Core,
with a data register at 8.8 pt for the valuation/evidence/fee documents and a letter
register at 10 pt for expert reports. Because Chromium is the renderer (ADR 0001),
the design system's CSS is consumed directly with no translation step.

## Consequences

- Pixel-faithful reproduction of the established brand, with the exact reds, charcoals,
  greys, zebra striping, grid lines and A4 metrics, because the renderer applies the brand's
  own CSS.
- Visual changes are made by editing CSS that the brand already maintains, keeping the
  renderer in step with the wider design system rather than forking the look into C#.
- A single source of styling truth (`report.css`) shared by every template and host.
- The product is tied to a CSS-capable renderer; this is exactly why headless Chromium was
  chosen (ADR 0001) and why QuestPDF/PdfSharp were rejected.

## Alternatives considered

- **Re-implement the look in a layout API (QuestPDF/PdfSharp):** would mean translating an
  existing, working CSS design system into imperative C#, risking subtle drift from the
  approved house style and duplicating maintenance. Rejected; this is also why those engines
  were rejected in ADR 0001.
- **A bespoke in-house stylesheet diverging from `collision-engineers-design`:** would fork
  the brand and invite inconsistency with other firm materials. Rejected in favour of reusing
  the canonical design system.
