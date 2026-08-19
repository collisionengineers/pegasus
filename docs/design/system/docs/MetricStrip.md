---
category: Metrics
---

A single row of compact `Metric` tiles (`.metric-strip`). Seven columns is the operations default (`metric-strip`), five is `secondary` (today and this week), three is the dashboard (`metric-strip--3`). The strip reflows to 4/2/1 columns as the viewport narrows; the 3-column dashboard strip keeps three. Use it wherever a screen opens with the counts that matter, each tile a link to the exact filtered list behind it.

**Rules**

- Put a `SectionLabel` above each strip (`Active cases`, `E-mail activity`, `Today and this week`) and nothing else: no lede, no page narration — the numbers are the explanation.
- Every tile carries its exact queue label in words; the state channel (`data-state` on the `Metric`) only colours the rail and icon.
- A section whose count does not exist is not shipped; do not pad a strip with placeholder tiles.
- Wide by nature: expect the full row at desktop widths and let it reflow rather than shrinking the tiles.

**Examples**

```tsx
<SectionLabel>Active cases</SectionLabel>
<MetricStrip columns={3}>
  <Metric label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="/Triage?queue=not_ready" />
  <Metric label="Review" icon="info" state="review" value={12} href="/Triage?queue=review" />
  <Metric label="Held" icon="clock" state="held" value={3} href="/Triage?queue=held" />
</MetricStrip>

<SectionLabel>E-mail activity</SectionLabel>
<MetricStrip columns={3}>
  <Metric label="Received today" icon="file-text" value={41} href="/Mail" />
  <Metric label="Unidentified" icon="alert-triangle" state="unidentified" value={4} />
  <Metric label="Blocked" icon="alert-circle" state="blocked" value={0} />
</MetricStrip>
```
