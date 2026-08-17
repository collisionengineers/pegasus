---
category: Forms
---

A labelled control cell for `FormGrid` — the `.form-grid > div` shape: bold small `<label htmlFor>`, then the control (`Input`, `Select`, `Textarea`), then an optional `.field-hint` (`<small id="{htmlFor}-hint">`) and an optional `.field-validation-error`. `wide` spans the whole grid row. Use it for every field in a grid; it has no styling of its own outside `FormGrid` / `FormPanel`.

**Rules**

- `htmlFor` must equal the control's `id`; when passing `hint`, also give the control `aria-describedby="{htmlFor}-hint"` — the cell renders the hint's id but the control is yours.
- A hint states one requirement, once, beside the field it governs (`Required. Chosen from the guide evidence above.`); it is not help text or a lede.
- `error` is the field-level message in the red-dark tone; keep it a sentence naming what to enter (`Enter a UK registration, for example LM19 KXR.`) and set `aria-invalid` on the control.
- Read-only or disabled values still sit in a `Field` with the same label so the grid stays aligned; the control recesses onto paper.

**Examples**

```tsx
<Field label="Retail value (£)" htmlFor="retail" hint="Required. Chosen from the guide evidence above.">
  <Input id="retail" type="number" min={0} step="0.01" inputMode="decimal" aria-describedby="retail-hint" />
</Field>

<Field label="Registration" htmlFor="reg" error="Enter a UK registration, for example LM19 KXR.">
  <Input id="reg" defaultValue="LM19-KXR" aria-invalid="true" />
</Field>

<Field label="Inspection address" htmlFor="address" wide>
  <Input id="address" defaultValue="Image Based Assessment" readOnly />
</Field>
```
