---
category: Record
---

The record container's sticky action bar (`.record__bar`), sitting under the head band: every action valid for the current state as compact `Button`s on the left, and the record-level commitment (`Button variant="dark"`) right-aligned in the `end` slot behind a 1px hairline rule (`.record__bar-rule`). It is the second child of `Record`, before `Tabs`/`RecordBody`.

**Rules**

- Show only the actions valid now; the operator must not scroll to find them. The `end` slot holds one committed action (Export, Complete) — never two.
- A disabled action states its condition (`<Button disabled condition="Available in Review">`) rather than disappearing; keep it in place so the operator knows it will exist.
- Use `variant="dark"` for the commitment and the default hairline button for state actions. Collision red (`primary`) is not used in the bar.
- Omit `end` when the record has no commitment; the rule is drawn only with the slot.

**Examples**

```tsx
<RecordBar end={<Button variant="dark">Export</Button>}>
  <Button>Actions</Button>
  <Button>Original case</Button>
</RecordBar>

<RecordBar
  end={
    <Button variant="dark" href="/Cases/CE-2026-01507/Export" disabled condition="Available in Review">
      Export
    </Button>
  }
>
  <Button>Actions</Button>
  <Button>Record registration</Button>
</RecordBar>
```
