---
category: Metrics
---

A queue tile (`.queue-card`): an optional Lucide icon in a tinted 34px square, the queue label, a big tabular count, an optional muted `detail` line, and — when linked — a trailing chevron with a hover fill. The 3px top rail takes the state colour from `data-state`. Use it inside a `QueueGrid` for the queues an operator opens; it is an `<a>` with `href`, otherwise an `<article>`.

**Rules**

- Prefer `state` (`needs-sorting`, `blocked`, `not-ready`, `review`, `held`, `completed`) over the legacy `theme` modifiers; green (`completed`) is for confirmed completion only.
- The label is the settled queue text; the rail and icon tint never carry meaning on their own.
- `unavailable` renders a quiet em dash on a neutral rail so a real count is always the loudest value in the grid; say why in `detail` (`Count not available`).
- Children go under the count — a `StatusChip` for the state the queue is waiting on, kept as an inline pill.
- Do not stack more than one line of `detail`; a card is a count with a destination, not a summary panel.

**Examples**

```tsx
<QueueCard label="Unidentified" icon="alert-triangle" state="unidentified" value={4} detail="Oldest 3 days" href="/Unidentified" />

<QueueCard label="Held" icon="clock" state="held" value={3} href="/Triage?queue=held">
  <StatusChip state="Awaiting information" />
</QueueCard>

<QueueCard label="Awaiting instruction" icon="clock" unavailable detail="Count not available" href="/Images?associated=no" />
```
