---
category: Actions
---

`.button-row` — a wrapping flex row of actions with the 10px gap. It holds a form's `PrimaryAction` and `SecondaryAction`, or a run of compact `Button`s. `end` right-aligns the row (`.button-row--end`) as in dialog footers; `sectionGap` adds `.section-gap` above when the row follows a form section.

**Rules**

- Primary first, then the secondary, in reading order; in an `end` row the primary sits at the far right.
- One primary per row and per screen; everything else is a `SecondaryAction` or a compact `Button`.
- Keep a disabled action in the row with its `condition` rather than removing it.
- Do not use it for record-level actions — those live in `RecordBar`.

**Examples**

```tsx
<ButtonRow>
  <PrimaryAction>Save changes</PrimaryAction>
  <SecondaryAction type="button">Cancel</SecondaryAction>
</ButtonRow>

<ButtonRow end>
  <SecondaryAction type="button">Cancel</SecondaryAction>
  <PrimaryAction>Reopen case</PrimaryAction>
</ButtonRow>
```
