---
category: Shell
---

`.refresh` — the compact corner freshness element: "Updated 14 Aug 09:32 London" in muted tabular text, a `StatusChip` only when the query is not current (`Refreshing`, `Stale`, `Partial`, `Unavailable`, `Failed`), and the 26px manual refresh button. It is a `role="status"` live region; `status="loading"` adds `.is-refreshing`, which spins the icon while the chip still carries the state in text. Use it in `PageHeading`'s `refresh` slot on dashboards, queues and any surface that shows query results.

**Rules**

- Always show the last-good time; only omit `updatedAt` when nothing has ever loaded ("Never updated").
- `current` earns no chip; every other status earns exactly one, so the state is never carried by the spinner alone.
- Refresh reruns the same filter and keeps last-good data visible; it never claims an external action succeeded.
- Do not put freshness copy elsewhere on the same surface (no duplicated "Last updated" line) and do not auto-poll.

**Examples**

```tsx
<PageHeading title="Dashboard" refresh={<Refresh updatedAt="14 Aug 09:32" onRefresh={reload} />} />

<Refresh updatedAt="14 Aug 08:05" status="stale" onRefresh={reload} />
<Refresh updatedAt="14 Aug 09:32" status="loading" />
<Refresh />
```
