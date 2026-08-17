---
category: Forms
---

`.form-grid` — the auto-fit field grid: as many 240px-minimum columns as fit, 12px row and 16px column gaps. Its children are `Field` cells (label, control, hint, validation); a `Field wide` spans the full row. Use it inside a `FormPanel` (usually `wide`) or a table-side workbench whenever a form has more than a couple of fields.

**Rules**

- Children are `Field`s (or a bare `<label>` wrapping its control); the grid gives each cell `min-width: 0` so long values never push the columns.
- Put the reason or free-text control last as `Field wide`; keep short scalar fields in the flow so they pack.
- `sectionGap` adds the section spacing above when the grid follows another block in the same panel.
- Do not force column counts with inline widths — narrow the container instead, and the grid collapses to one column on its own.

**Examples**

```tsx
<FormGrid>
  <Field label="Claimant" htmlFor="claimant"><Input id="claimant" defaultValue="J. Okafor" /></Field>
  <Field label="Registration" htmlFor="reg" hint="As printed on the V5C.">
    <Input id="reg" defaultValue="YD68 TFA" aria-describedby="reg-hint" />
  </Field>
  <Field label="Principal" htmlFor="principal">
    <Select id="principal"><option>AXA</option><option>Direct Line</option></Select>
  </Field>
  <Field label="Reason" htmlFor="reason" wide>
    <Textarea id="reason" rows={3} placeholder="Why the recorded values are being changed." />
  </Field>
</FormGrid>
```
