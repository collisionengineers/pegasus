---
category: Record
---

Compact fact columns inside a record body (`.facts`): each group is a `<section>` with an uppercase muted title over a `<dl>` of 28px rows — 100px muted term, bold tabular value. Columns are auto-fit at 230px minimum, so two or three groups sit side by side and wrap on a narrow body. Use it for the identity facts of a record (vehicle, instruction, progress); use `DataRow` for fields that need provenance or an action.

**Rules**

- Two or three groups with 3–5 items each; more belongs in a `DetailList` or table.
- Mark a fact that is not yet set with `quiet: true` and words (`Not assigned`, `Not yet`) — never an empty value.
- Terms are short nouns (`Registration`, `Claim`, `Received`); values are settled business values and office times.
- The group title is the only heading; do not add a section label above the block.

**Examples**

```tsx
<Facts
  groups={[
    { title: 'Vehicle', items: [
      { term: 'Registration', value: 'LM19 KXR' },
      { term: 'Make', value: 'Volkswagen' },
      { term: 'Mileage', value: '48,210' },
    ] },
    { title: 'Instruction', items: [
      { term: 'Principal', value: 'AXA' },
      { term: 'Received', value: '12 Aug 09:14' },
      { term: 'Engineer', value: 'Not assigned', quiet: true },
    ] },
  ]}
/>
```
