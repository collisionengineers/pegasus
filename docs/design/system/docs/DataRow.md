---
category: Record
---

One field/value line inside a record body (`.datarow`): a 150px muted field label, a 210px bold tabular value, and an `end` slot pushed right for the `Provenance` icon or a compact `Button`. Omitting `value` renders the quiet `Not recorded`; `suggested` adds `Suggested …` beside an unrecorded value. Rows stack with a top hairline each; use several in sequence.

**Rules**

- Never leave the value blank: an unrecorded field is `Not recorded` (omit `value`); a suggestion is only shown while nothing is recorded.
- `end` is supplementary — the row must make sense with it ignored. Provenance is one word (`Staff`, `Extracted`, `AI`, `E-mail`, `Lookup`).
- Put an action (`Change`, `Record`) in `end` as a default `Button`; do not put a link inside the value.
- Field labels are business nouns; suggested values are stated as values, not as engine output.

**Examples**

```tsx
<DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
<DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
<DataRow field="Repairer" end={<Provenance word="Staff" />} />
<DataRow field="Inspection address" value="14 Ridgeway Close, Reading" end={<Button>Change</Button>} />
```
