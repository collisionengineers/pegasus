---
category: Record
---

A read-only guide figure on the recessed paper ground (`.evidence-figure`): an uppercase muted label, a bold tabular value at h3 size, and an optional muted `source` line stacked underneath. Use a row of them on the assessment screen for valuation evidence — CAP and Glass's retail/trade figures, mileage adjustments — that the engineer reads but does not edit here.

**Rules**

- Lay several out in a grid of equal columns; each tile is one figure with one label.
- Give every figure its `source` (`Glass's guide, 12 Aug`); a figure with no provenance is not evidence.
- No figure yet is an em-dash value with visually-hidden text and the source `No figure recorded` — never a hidden tile or a blank.
- Values are formatted amounts (`£14,250`); figures never carry colour or a state.

**Examples**

```tsx
<div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 12 }}>
  <EvidenceFigure label="CAP retail" value="£14,250" source="CAP, 12 Aug" />
  <EvidenceFigure label="Glass's trade" value="£12,900" source="Glass's guide, 12 Aug" />
  <EvidenceFigure
    label="CAP trade"
    value={<><span aria-hidden="true">—</span><span className="vh">No value</span></>}
    source="No figure recorded"
  />
</div>
```
