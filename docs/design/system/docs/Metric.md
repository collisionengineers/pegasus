---
category: Metrics
---

One compact tile in a `MetricStrip` (`.metric`): the queue or figure label with a small Lucide icon, the count large at the bottom, and a 3px state rail on top. With `href` it renders as an `<a>` that opens the exact filtered list behind the number; without, a plain `<span>`. Use it for counts an operator scans and acts on, never for decoration.

**Rules**

- `label` is the settled queue text (`Not ready`, `Needs sorting`, `Received today`); `state` sets `data-state` and drives only the rail and icon tint — the words carry the meaning.
- A composed count of zero renders `0`. When the datum is absent, pass `absent="Unavailable"` (or `Refreshing`, `Stale`) so the state replaces the value; never a dash pretending to be a number.
- Link every metric that has a list behind it; leave `href` off only when no destination exists.
- Icons come from the Pegasus sprite (`alert-triangle` for not-ready/needs-sorting, `info` for review, `clock` for held, `alert-circle` for blocked, `file-text`, `arrow-right`, `upload`).

**Examples**

```tsx
<Metric label="Review" icon="info" state="review" value={12} href="/Triage?queue=review" />
<Metric label="Reports sent today" icon="upload" value={0} />
<Metric label="Awaiting instruction" icon="clock" absent="Unavailable" />
```
