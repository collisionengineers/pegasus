---
category: Record
---

A block header inside a record body: `.blockhead` is a flex row with the block `title` as an uppercase, muted, letter-spaced `<h2>` on the left and optional `end` controls pushed to the right in `.blockhead-end`. It names a block of `DataRow`s or a `FieldGrid` (Vehicle, Instruction, Evidence) and holds the block-level control, such as Edit, on the same line so the body stays a single reading column.

**Rules**

- `title` is a short noun (Vehicle, Instruction); do not add a lede or description under it.
- `end` takes compact `Button`s or a `StatusChip`; keep record-level commitments in the `RecordBar`, not here.
- One `Blockhead` per block, directly above its rows; it does not wrap the rows.
- Because it renders an `<h2>`, keep heading order sensible inside `RecordBody`.

**Examples**

```tsx
<Blockhead title="Vehicle" />
<DataRow field="Registration" value="LM19 KXR" end={<Provenance word="Extracted" />} />

<Blockhead title="Instruction" end={<Button>Edit</Button>} />
```
