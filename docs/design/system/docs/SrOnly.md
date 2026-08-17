---
category: Layout
---

Visually hidden text for assistive technology: `.sr-only` is a `<span>` clipped to a 1px box (absolute, overflow hidden, clip rect, nowrap) so it is read by screen readers but never seen. Use it to keep a semantic label or caption available — a table's purpose, a sort order, the meaning of an icon-only control — without adding a duplicate visual heading to a dense operational surface.

**Rules**

- Text only, one short phrase; it renders nothing visible by design.
- Prefer visible text where the design allows one; `SrOnly` is for cases where a visible label would repeat what the layout already shows.
- Never hide state or a consequence in it — state is carried by visible chip text and a disabled action states its visible condition.
- Do not put focusable controls inside it.

**Examples**

```tsx
<h2><SrOnly>Cases awaiting review, sorted by received date</SrOnly></h2>

<button type="button"><Icon name="refresh-cw" /><SrOnly>Refresh</SrOnly></button>
```
