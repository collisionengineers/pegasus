---
category: Status
---

`.prov` — where a value came from: a 22px muted icon (`role="img"`, focusable) whose tooltip on hover and keyboard focus is exactly one word — `Staff`, `Extracted`, `AI`, `E-mail`, `Lookup`, `Principal` or `Automatic` — with a default Lucide glyph per word. Always supplementary: the row must make sense with the icon ignored. It normally sits in the `end` slot of a `DataRow`.

**Rules**

- `word` is one of the seven settled words; the tooltip and `aria-label` are that word and nothing more.
- Never the only signal: the value, its `Suggested` state and the field label carry the meaning on their own.
- Do not add text beside it, restyle it as a chip, or use it to convey state (that is `StatusChip`).
- Override `icon` only when the default glyph would mislead; keep to the sprite's Lucide set.

**Examples**

```tsx
<DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
<DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
<DataRow field="Claimant" value="J. Okafor" end={<Provenance word="E-mail" />} />
```
