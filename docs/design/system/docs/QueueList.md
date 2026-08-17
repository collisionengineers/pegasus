---
category: Metrics
---

A panel-like list (`.panel.queue-list`) where every actionable row is one full-row link with a visible trailing `›`, a hairline between rows and a red left rail on hover — never a nested link buried in a cell. Use it for a queue of records (Held cases, Triage, the mail workspace) where each row opens one record; use a data table when the operator compares columns.

**Rules**

- Children are `QueueListRow`s only; put `QueueFilters` above the list, not inside it.
- Left column is identity (reference in bold, one muted line of principal · registration · reason); right column is state — a `StatusChip` plus a small line such as `Next chase 18 Aug`, or a `<time>`.
- Mail rows add a `middle` column (subject + excerpt) and mark unread rows with `state="unread"` and the word `Unread` in the row — weight alone is not a state.
- Rows without a destination render as `<article>` and get no `›`; do not fake a link.
- The list carries no heading of its own; the section label or panel above it names the queue.

**Examples**

```tsx
<QueueList>
  <QueueListRow
    href="/Cases/CE-2026-01432"
    title="CE-2026-01432"
    subtitle="AXA · LM19 KXR · Waiting on repairer images"
    end={<><StatusChip state="Awaiting information" /><small>Next chase 18 Aug</small></>}
  />
  <QueueListRow
    href="/Cases/CE-2026-01418"
    title="CE-2026-01418"
    subtitle="Direct Line · YD68 TFA · Total-loss valuation queried"
    end={<><StatusChip state="Held" /><small>Next chase 17 Aug</small></>}
  />
</QueueList>
```
