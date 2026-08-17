---
category: Data
---

The operational table: `.table-wrap` > `<table>` with 32px rows, uppercase muted headers on paper, hairline row rules, tabular numerals and a hover wash. Links in cells are navy and bold. Use it for every list of records — the Queues table, case results, principals — driven by `columns` (header + `cell` renderer) and `rows`; it renders the muted `empty` sentence instead of an empty grid.

**Rules**

- Always name the table: pass `caption` (visually hidden `.vh` by default; `captionVisible` when the screen needs it) — never leave assistive technology with an anonymous grid.
- The first column is the record's identity as a link (registration on a Triage row, case reference on a case row); state is a `StatusChip` in its own column, never colour alone.
- Mark numeric columns `tabular` so figures align, and put a calculated total in `footer` (same length as `columns`) rather than a last data row.
- `empty` is one business sentence (`No cases are ready to confirm.`); never render zero rows with headers, and never describe queue or refresh mechanics.
- Page it with `Pager` beneath; never infinite scroll. `lineGrid` is only for the repair-specification layout.

**Examples**

```tsx
<DataTable
  caption="Triage work"
  columns={[
    { header: 'Registration', cell: (r) => <a href={r.href}>{r.registration}</a> },
    { header: 'Opened', cell: (r) => <time>{r.opened}</time> },
    { header: 'State', cell: (r) => <StatusChip state={r.state} /> },
    { header: 'Assigned to', cell: (r) => r.assignee },
  ]}
  rows={rows}
  empty="No triage work is open."
/>

<DataTable
  caption="Repair estimate lines"
  columns={[
    { header: 'Description', cell: (r) => r.description },
    { header: 'Hours', cell: (r) => r.hours, tabular: true },
    { header: 'Total', cell: (r) => r.total, tabular: true },
  ]}
  rows={items}
  footer={[<b>Estimate total</b>, <b>5.6</b>, <b>£331.20</b>]}
/>
```
