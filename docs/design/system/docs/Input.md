---
category: Forms
---

`<input>` — the stylesheet's text control: full width of its cell, 34px tall, hairline `line-strong` border, 5px radius, panel background, small text. Read-only and disabled inputs recess onto paper with the lighter `line` border and muted text. Use it inside a `Field` (or a `.form-grid` / `.form-panel` label) for every text, number, date, e-mail and file entry.

**Rules**

- Always labelled: place it in a `Field` with `htmlFor` = `id`, or as the child of a `<label>` inside `FormGrid` / `FormPanel`; a bare input outside those has no label styling.
- Use `readOnly` for a value the operator may copy but not change (a reference, a one-time secret) and `disabled` for a control that is not available now; both keep the value legible.
- Native `type="date"`, `type="number"` and `type="file"` are used as-is; state the accepted files or range in a `.field-hint`, never only in an `accept` attribute.
- Placeholders describe the entry (`Name as instructed`); they never replace the label.

**Examples**

```tsx
<Field label="Registration" htmlFor="reg"><Input id="reg" name="vehicleRegistration" defaultValue="LM19 KXR" /></Field>
<Field label="Reference" htmlFor="ref"><Input id="ref" value="CE-2026-01432" readOnly /></Field>
<Field label="Incident date" htmlFor="incident"><Input id="incident" type="date" defaultValue="2026-08-06" /></Field>
```
