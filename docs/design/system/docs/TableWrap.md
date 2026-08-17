---
category: Data
---

`.table-wrap` — the bordered, rounded, horizontally scrolling wrapper for a hand-written `<table>`. Use it when a table does not fit the `DataTable` column model (row-scoped headers, row groups, mixed cells) but must still look like every other Pegasus table: uppercase muted `th` on paper, hairline row rules, navy bold links.

**Rules**

- Write the table with plain `<th scope="col">`, `<td>`, `<time>` and `.tabular` cells — site.css styles the elements, so no extra classes are needed.
- Give the table a `<caption className="vh">` naming it for assistive technology.
- Prefer `DataTable` for ordinary record lists; `TableWrap` is the escape hatch, not the default.
- Never remove the wrapper to save space — it provides the border and the overflow scroll on narrow screens.

**Examples**

```tsx
<TableWrap>
  <table>
    <caption className="vh">Principals</caption>
    <thead>
      <tr><th scope="col">Principal</th><th scope="col">Open cases</th><th scope="col">Last instruction</th></tr>
    </thead>
    <tbody>
      <tr><td><a href="/Principals/axa">AXA</a></td><td className="tabular">42</td><td><time>14 Aug 08:52</time></td></tr>
    </tbody>
  </table>
</TableWrap>
```
