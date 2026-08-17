---
category: Forms
---

`.panel.form-panel` — the standard form section: an optional `.section-label` title, then (when `form` is set) a `<form>` whose direct children stack in a 12px grid. Its width is capped at 45rem so a single-column form stays readable; `wide` removes the cap for a `FormGrid` inside. Use it for every page-level form (Upload, New case, Administration) and for a titled section holding one control; use `Panel` for read-only content.

**Rules**

- Pass `form={{ … }}` when children are controls: the panel renders the `<form>` and its grid; without it the panel is a plain section.
- One `PrimaryAction` per screen; a page-level form ends with it (optionally inside a `ButtonRow` so it does not stretch to the grid width).
- Direct-child `<label>`s are the bold small labels; a hint goes in `.field-hint` beside the field it governs, stated once. Prefer `FormGrid` + `Field` for more than two or three fields.
- No lede or subtitle under the title: the section label names the form and the fields explain themselves.

**Examples**

```tsx
<FormPanel title="Upload a document" form={{ encType: 'multipart/form-data' }}>
  <label htmlFor="upload">
    Drag a file here, or browse
    <span className="field-hint">E-mail, Word document, PDF or image — up to 25 MB.</span>
  </label>
  <Input id="upload" name="Upload" type="file" />
  <PrimaryAction>Upload</PrimaryAction>
</FormPanel>

<FormPanel title="Details" wide form={{}}>
  <FormGrid>…</FormGrid>
  <ButtonRow><PrimaryAction>Create case</PrimaryAction></ButtonRow>
</FormPanel>
```
