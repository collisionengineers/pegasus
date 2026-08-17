---
category: Metrics
---

An auto-fit grid of `QueueCard`s (`.queue-grid`, minimum 220px per card) that lets a queue overview take as many columns as the width allows and wraps the rest. Use it at the top of a queue workspace (Inbox, Triage) where each card is a queue the operator can open; use `MetricStrip` instead for the compact fixed-column dashboard.

**Rules**

- Children are `QueueCard`s only; give each a `state` so its rail matches the queue it stands for, and an `href` to the queue list.
- Three or four cards is the usual count; the grid wraps at narrow widths rather than shrinking cards below 220px.
- A queue whose count is not available still gets a card (`unavailable`) — the operator learns the queue exists and the count is absent.
- No heading inside the grid; the section label above it names the group.

**Examples**

```tsx
<QueueGrid>
  <QueueCard label="Needs sorting" icon="alert-triangle" state="needs-sorting" value={4} detail="Oldest 3 days" href="/Inbox?queue=needs_sorting" />
  <QueueCard label="Blocked" icon="alert-circle" state="blocked" value={1} href="/Inbox?queue=blocked" />
  <QueueCard label="Review" icon="info" state="review" value={12} detail="Oldest 2 days" href="/Triage?queue=review" />
</QueueGrid>
```
