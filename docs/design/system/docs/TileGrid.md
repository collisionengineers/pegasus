---
category: Metrics
---

A two-column grid of `MetricTile`s that share hairline borders (`.tile-grid`): the tiles overlap by 1px so the group reads as one bordered block rather than separate cards. Use it inside a panel or record body for a small set of figures about one thing (a case, a principal, a week); it collapses to one column under 720px.

**Rules**

- Children are `MetricTile`s only; keep the set to two, four, or a pair plus a `span` tile so no cell is left empty.
- Give the tile that needs the operator's attention `attention` (amber); everything else stays neutral — green is not used here.
- Use `span` for the one figure that summarises the others (a total, a month), never to fill space.
- This is a figures block, not a navigation surface: link tiles only when a filtered list exists behind the number.

**Examples**

```tsx
<TileGrid>
  <MetricTile label="Open cases" value={124} icon="file-text" href="/Cases" />
  <MetricTile label="Awaiting information" value={9} icon="clock" attention href="/Cases?state=awaiting" />
  <MetricTile label="Reports sent this month" value={118} icon="upload" span />
</TileGrid>
```
