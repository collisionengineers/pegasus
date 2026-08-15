---
category: Data
---

`.panel.filterbar` — one line of common filters above a results table, with the rarely used fields behind a `More filters` disclosure. It renders a `<section aria-label>` with a visually hidden `<h2>` and a GET form: `.filterbar__line` for the controls, then an optional `<details>` holding a `FormGrid`. Eleven inputs stacked above the results is a form, not a filter.

**Rules**

- The line is: keyword `Input` (flexes wide), one or two `Select`s (auto width), then `Button variant="dark"` "Search" and a plain `Button` "Clear" — in that order.
- Every line control has a visually hidden `<label className="vh">`; the disclosure fields use `Field` in a `FormGrid` with visible labels.
- Pass `moreOpen` when an advanced filter is active so the operator can see why the results are narrowed.
- Placeholders name what may be typed (`Case/PO, registration, claimant or claim number`), never instructions; option text uses the settled stage names (`Not ready`, `Review`, `Held`).

**Examples**

```tsx
<FilterBar title="Filter cases" more={
  <FormGrid>
    <Field label="Registration" htmlFor="f-registration"><Input id="f-registration" name="registration" /></Field>
    <Field label="Received on" htmlFor="f-received"><Input id="f-received" name="received" type="date" /></Field>
  </FormGrid>
}>
  <label htmlFor="case-query" className="vh">Case/PO or keyword</label>
  <Input id="case-query" name="query" placeholder="Case/PO, registration, claimant or claim number" />
  <label htmlFor="case-state" className="vh">Case stage</label>
  <Select id="case-state" name="state"><option value="">Any stage</option><option>Review</option></Select>
  <Button variant="dark" type="submit">Search</Button>
  <Button href="/Cases">Clear</Button>
</FilterBar>
```
