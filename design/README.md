# Design system

## Governed surfaces

The current exercised surface is the Development-only Razor Pages dashboard,
manual intake upload, queue, and receipt review. The planned V1 staff surfaces
are Operations, Intake, Triage, Cases, and authorized Administration; their
shell direction remains unapproved.

## Authority and source map

| Concern | Canonical design source | Runtime consumer/output |
| --- | --- | --- |
| product interaction/accessibility | [`docs/plans/ui-ux/requirements.md`](../docs/plans/ui-ux/requirements.md) and [`ui-spec.md`](../docs/plans/ui-ux/ui-spec.md) | planned V1 Razor Pages; current proof is narrower |
| brand/style | [`brand/style.md`](brand/style.md) | `src/CollisionSpike.Web/Pages/Shared/_Layout.cshtml`, `src/CollisionSpike.Web/wwwroot/css/site.css` |
| tokens | [`tokens/README.md`](tokens/README.md) | `src/CollisionSpike.Web/wwwroot/css/site.css` |
| components/patterns | [`components/index.md`](components/index.md), [`patterns/index.md`](patterns/index.md) | current Razor Pages under `src/CollisionSpike.Web/Pages/` |
| comparison references | [`references/README.md`](references/README.md) | selection aids only; no runtime output |

## Navigation

- [Brand style](brand/style.md), [imagery](brand/imagery.md), and [logos](brand/logos/README.md)
- Foundations: [colour](foundations/colour.md), [typography](foundations/typography.md), [spacing/layout](foundations/spacing-and-layout.md), [motion](foundations/motion.md), and [accessibility](foundations/accessibility.md)
- [Tokens](tokens/README.md), [icons](assets/icons/README.md), and [fonts](assets/fonts/README.md)
- [Components](components/index.md), [patterns](patterns/index.md), and [references](references/README.md)

## Change rule

Update approved design authority, source/runtime mappings, and affected
implementation in one reviewed change. Do not add synthetic brand assets,
operational examples, copy, or duplicated generated output. Every V2/V3/V3+
UI capability re-enters planning/design approval rather than inheriting V1.
