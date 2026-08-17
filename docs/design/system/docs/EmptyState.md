---
category: Status
---

`.empty-state` — a muted `<p>` of business-language copy for a zero result: no cases match, nothing is waiting, no evidence yet. It takes the place of the list or table it stands in for, inside the same panel or section, and says why the space is empty in the operator's terms.

**Rules**

- One short sentence in the operator's language (`No cases match these filters.`), never a technical reason or the word "intake".
- Render it where the rows would have been — inside the panel, under its heading — not as a page-level card.
- Muted text only; no icon, no illustration, no green (an empty result is not a completion).
- If the emptiness is a failure or a stale query, that is `StatusCard`/`Refresh`, not an empty state.

**Examples**

```tsx
<Panel>
  <h2>Awaiting information</h2>
  <EmptyState>Nothing is waiting on a repairer or principal right now.</EmptyState>
</Panel>

<EmptyState>No cases match these filters.</EmptyState>
```
