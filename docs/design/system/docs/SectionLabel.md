---
category: Layout
---

An eyebrow-styled `<h2>` that names a section of a panel: `.section-label` is small, uppercase, muted and letter-spaced with a short bottom margin, so it labels the content beneath without competing with the record's identity. Pass `icon` (a Lucide glyph name from the DS `Icon` set) to render `.section-label--iconed`, a flex row with the glyph before the text — for a section such as Outstanding or Send to Claude.

**Rules**

- Use it as the first child of a `Panel` or workbench section; the label is the section's heading, so wire `aria-labelledby` to it when the section is a landmark.
- Text is a short business noun (Result, Missing fields, Vehicle) — no sentence, no lede beneath.
- `icon` is decoration alongside the text, never a substitute for it.
- Do not use it as a page heading (`Eyebrow` + `h1`) or as a block header inside a record body (`Blockhead`).

**Examples**

```tsx
<Panel>
  <SectionLabel>Result</SectionLabel>
  <DataRow field="Registration" value="LM19 KXR" />
</Panel>

<SectionLabel icon="alert-triangle">Outstanding</SectionLabel>
```
