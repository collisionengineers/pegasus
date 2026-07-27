# Design system

## Governed surfaces

The current exercised surface is the Development-only Razor Pages dashboard,
manual intake upload, queue, and receipt review. The planned V1 staff surfaces
are Operations, Intake, Triage, Cases, and authorized Administration; their
shell direction remains unapproved.

The provided `collision-engineers-design-dev` bundle was the upstream Collision
Engineers visual foundation. It explicitly excludes an internal command-centre
application, so this repository adopts only the shared brand essentials the
user requested: the exact master logo, website red, warm charcoal, near-black,
system UI type, 2px geometry, border-first depth, visible focus, and Lucide-only
icon rule. Marketing layouts, imagery, fonts, WhatsApp, document/letterhead
systems, signatures, scroll reveals, and mobile navigation are not imported.
The source bundle is not retained as a second design system.

## Authority and source map

| Concern | Canonical design source | Runtime consumer/output |
| --- | --- | --- |
| product interaction/accessibility | [`product/requirements.md`](product/requirements.md), [`ui-spec.md`](product/ui-spec.md), and [`traceability-matrix.md`](product/traceability-matrix.md) | planned V1 Razor Pages; current proof is narrower |
| brand/style and master logo | [`brand/style.md`](brand/style.md), [`brand/logos/README.md`](brand/logos/README.md) | approved source for future V1 UI; current Development layout has not adopted it |
| adapted tokens | [`tokens/README.md`](tokens/README.md) | approved design values; current `src/CollisionSpike.Web/wwwroot/css/site.css` is recorded divergence |
| components/patterns | [`components/index.md`](components/index.md), [`patterns/index.md`](patterns/index.md) | current Razor Pages under `src/CollisionSpike.Web/Pages/` |
| comparison references | [`references/README.md`](references/README.md) | selection aids only; no runtime output |

## Navigation

- [Brand style](brand/style.md), [imagery](brand/imagery.md), and [logos](brand/logos/README.md)
- Foundations: [colour](foundations/colour.md), [typography](foundations/typography.md), [spacing/layout](foundations/spacing-and-layout.md), [motion](foundations/motion.md), and [accessibility](foundations/accessibility.md)
- [Tokens](tokens/README.md), [icons](assets/icons/README.md), and [fonts](assets/fonts/README.md)
- Product UI: [requirements](product/requirements.md), [specification](product/ui-spec.md), and [traceability](product/traceability-matrix.md)
- [Components](components/index.md), [patterns](patterns/index.md), and [references](references/README.md)

## Change rule

Update approved design authority, source/runtime mappings, and affected
implementation in one reviewed change. Never redraw the gear-C logo. Do not add
synthetic brand assets, operational examples, copy, or duplicated generated
output. Every V2/V3/V3+ UI capability re-enters planning/design approval rather
than inheriting V1.
