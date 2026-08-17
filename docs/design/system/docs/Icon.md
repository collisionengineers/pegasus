---
category: Layout
---

A Lucide line icon rendered inline with the `.icon` class: 2px stroke, round caps, `currentColor`, no fill. The sixteen glyphs are the ones the operator interface ships in its sprite — `search`, `user`, `refresh-cw`, `clock`, `calendar`, `check-circle`, `alert-triangle`, `alert-circle`, `info`, `file-text`, `filter`, `shield`, `chevron-right`, `arrow-right`, `upload`, `lock` — and no other icon set is used anywhere in Pegasus. Icons are decorative (`aria-hidden`) unless given a `label`.

**Rules**

- Sizes: `sm` (.875rem) inside chips and buttons, default 1.125rem in labels and rows, `lg` (1.25rem) in admin card squares. Do not scale by CSS.
- An icon never carries meaning alone: pair it with the state word (`Not ready`, `Held`); the state channel tints it through the parent (`.metric__label .icon`, `.queue-icon`).
- Pass `label` only when the icon is the sole content of a control (a search button); otherwise leave it decorative.
- `spin` adds `icon--spin`; it animates only inside a parent with `.is-refreshing`, so use it on refresh controls, not as a generic loader.
- Do not add glyphs from other sets or inline SVG of your own; if a glyph is missing it goes into the sprite first.

**Examples**

```tsx
<span className="metric__label"><Icon name="alert-triangle" size="sm" /> Not ready</span>
<Icon name="chevron-right" />
<Icon name="search" label="Search" />
<Icon name="refresh-cw" size="sm" spin />
```
