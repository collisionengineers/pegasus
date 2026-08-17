---
category: Layout
---

The small uppercase muted label above a heading or figure: `.eyebrow` is a `<p>` at eyebrow size, 700 weight, letter-spaced, with a short bottom margin. It gives a heading or a metric its context — the collection above a record title, or the name above a number — in one or two words.

**Rules**

- Sits directly above the thing it labels (`<Eyebrow>Cases</Eyebrow><h1>Case CE-2026-01432</h1>`); never on its own.
- One or two words of business context; not a sentence and not a lede.
- It is a paragraph, not a heading: it does not enter the heading outline, so keep the real `h1`/`h2` beneath it.
- For a section heading inside a panel use `SectionLabel`; for a record block use `Blockhead`.

**Examples**

```tsx
<Eyebrow>Cases</Eyebrow>
<h1>Case CE-2026-01432</h1>

<Eyebrow>In Review</Eyebrow>
<strong style={{ fontSize: 28 }}>12</strong>
```
