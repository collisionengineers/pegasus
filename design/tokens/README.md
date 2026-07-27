# Design tokens

- Upstream source: `styles/colors_and_type.css` in the provided `collision-engineers-design-dev` bundle; source pack not retained
- Canonical adapted owner: this inventory and the foundation documents in `design/foundations/`
- Format/generator: documented values; no generated token file or copied website stylesheet
- Current runtime: `src/CollisionSpike.Web/wwwroot/css/site.css` (partially aligned; not generated)

## Token groups

| Group | Adapted values retained for CollisionSpike |
| --- | --- |
| Brand | red `#DB0816`, red-dark `#8F1422`, red tint `rgba(219,8,22,.07)`, charcoal `#2C2A27`, ink `#16191D` |
| Surface | white `#FFFFFF`, light `#F5F4F2`, border `#E6E4E1`, muted `#6B6B6B` |
| State | success `#16833B`; amber incomplete/pending and navy Review remain owned by the CollisionSpike UI plan/current implementation until selected design reconciliation |
| Type | upstream system UI stack only; no brand-font files |
| Shape/focus | 2px primary radius, 1px borders, 3px `rgba(219,8,22,.38)` keyboard focus ring |
| Spacing | 4, 8, 12, 14, 18, 24, 32, 40, 64px; application uses only exercised steps |

Marketing-only website tokens, WhatsApp green/pill, large display scale, CTA
shadow, document red, and brand-font declarations are excluded. Do not create a
parallel runtime token file until a selected UI change can make one source
directly consumable.
