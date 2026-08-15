---
category: Forms
---

`<textarea>` — the multi-line control: 5rem minimum height, vertical resize only, same hairline border and small text as `Input`. Use it for a reason, note or free text; in a `FormGrid` it normally sits last as a `Field wide`.

**Rules**

- Labelled through a `Field` (`htmlFor` = `id`) or a `<label>` inside `FormGrid` / `FormPanel`.
- Set `rows` (3–4) so the empty control shows its size; the operator can still drag it taller.
- The placeholder says what is wanted (`Why this case is being put on hold.`), not an example value; a required reason is `required` and stated as such in the surrounding copy.
- Do not use it for a one-line value — that is an `Input`.

**Examples**

```tsx
<Field label="Reason" htmlFor="reason" wide>
  <Textarea id="reason" name="reason" rows={3} required placeholder="Why this case is being put on hold." />
</Field>
```
