---
category: Data
---

`.pager` — accessible Previous / context / Next pagination rendered as `<nav aria-label>`, placed under a `DataTable`. Pegasus pages results; it never infinite-scrolls. The links are hairline buttons; the context (`Page 3 of 7`, `Page 1 · showing 25`) is muted small text with tabular numerals.

**Rules**

- Omit `previousHref` on the first page and `nextHref` on the last — the link disappears entirely; it is never rendered disabled.
- Give `label` a name specific to the list (`Case result pages`, `Principal organization pages`) so several pagers on one page are distinguishable.
- Keep `context` to a position or count; do not add refresh mechanics there.
- Links carry `href` for real navigation; `onPrevious`/`onNext` are for embedded interactive lists only.

**Examples**

```tsx
<Pager label="Case result pages" context="Page 1 · showing 25" nextHref="?page=2" />
<Pager label="Case result pages" context="Page 3 of 7" previousHref="?page=2" nextHref="?page=4" />
```
