---
category: Record
---

One extracted field: `.field-card` is an `<article>` on the white panel ground with an uppercase muted `<h3>` title, the value in `<strong>`, and an optional muted `<small>` detail line for the source or time. `conflict` adds `.field-card--conflict`, a 3px amber left rail marking a value that disagrees with another source. Render inside a `FieldGrid`; on its own it has no border of its own.

**Rules**

- `title` is the field name (Registration, Claim number, Accident date, Claimant); the child is the value; `detail` is one short line — where it came from and when, or what the other source says.
- Use `conflict` only when two sources disagree, and say so in `detail` — the amber rail is a signal, the text is the meaning.
- Business language in the detail: `Extracted`, `E-mail`, `Staff` — never engine or pipeline terms.
- A card shows a value; it does not host inputs or buttons.

**Examples**

```tsx
<FieldCard title="Registration" detail="Extracted · 12 Aug 09:14">LM19 KXR</FieldCard>

<FieldCard title="Accident date" detail="E-mail says 4 Aug 2026" conflict>6 Aug 2026</FieldCard>
```
