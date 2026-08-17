---
category: Record
---

The extracted-fields grid: `.field-grid` lays `FieldCard`s out in auto-fit columns (min 260px each) separated by 1px hairlines — the gap shows the `--line` colour behind white cards, so the set reads as one ruled block rather than a row of floating boxes. Use it on a record body or review surface to show what was read from an instruction (registration, claim number, accident date, claimant) at a glance, one card per field.

**Rules**

- Children are `FieldCard`s only; each carries an uppercase title, the value, and an optional small `detail` (source and time, or the competing value).
- Mark a disagreeing value with `conflict` on that card — the amber rail plus the detail text says why, never the colour alone.
- Fill whole rows where you can (three or six cards at 960px): an orphan cell leaves the hairline background showing in the empty track.
- Field values are facts, not controls; put edit actions in the block's `Blockhead` or the record's `RecordBar`, not inside cards.
- Wide component: give it the record's full width (wrap at ~960px if it stands alone).

**Examples**

```tsx
<FieldGrid>
  <FieldCard title="Registration" detail="Extracted · 12 Aug 09:14">LM19 KXR</FieldCard>
  <FieldCard title="Claim number" detail="Extracted · 12 Aug 09:14">AX/44/210983</FieldCard>
  <FieldCard title="Accident date" detail="E-mail says 4 Aug 2026" conflict>6 Aug 2026</FieldCard>
</FieldGrid>
```
