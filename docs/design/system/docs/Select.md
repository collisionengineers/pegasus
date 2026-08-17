---
category: Forms
---

`<select>` — the same treatment as `Input`: full width, 34px, hairline `line-strong` border, 5px radius, small text; disabled recesses onto paper. Use it for a settled short list the operator picks one of (principal, inspection mode, rate class); for two or three options a `ChoiceGroup` of radios is often clearer.

**Rules**

- Labelled through a `Field` (`htmlFor` = `id`) or a `<label>` inside `FormGrid` / `FormPanel`.
- Offer a named neutral option where nothing is recorded yet (`Not recorded`), not an empty first line.
- Options are business terms in their settled casing (`Image Based Assessment`, `Physical inspection`), never internal codes.
- A disabled select keeps its value visible; state why elsewhere if the reason is not obvious.

**Examples**

```tsx
<Field label="Inspection mode" htmlFor="mode">
  <Select id="mode" name="inspectionMode" defaultValue="image">
    <option value="">Not recorded</option>
    <option value="image">Image Based Assessment</option>
    <option value="physical">Physical inspection</option>
  </Select>
</Field>
```
