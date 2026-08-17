---
category: Metrics
---

A bordered tile in a `TileGrid` (`.metric-tile`): a Lucide icon in a 34px square, the count in the metric size, and the label beneath it. Plain by default; `attention` switches the border and icon square to amber, `span` makes it take the full grid width, and `href` renders it as an `<a>`. Use it for a figure that belongs to a set, not for a standalone number (that is a `Metric` in a strip).

**Rules**

- `label` is a short noun phrase in business language (`Open cases`, `Awaiting information`, `Reports sent this month`); the count is `value`.
- `attention` names something the operator should act on and is the only tone available; it never replaces a state chip — the label says what the figure is.
- Do not use `span` for more than one tile in a grid, and only when the figure summarises the others.
- Always render inside `TileGrid`; a lone tile has no shared borders and looks unfinished.

**Examples**

```tsx
<MetricTile label="Open cases" value={124} icon="file-text" href="/Cases" />
<MetricTile label="Awaiting information" value={9} icon="clock" attention href="/Cases?state=awaiting" />
<MetricTile label="Reports sent this month" value={118} icon="upload" span />
```
