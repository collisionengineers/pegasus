---
category: Data
---

`.action-list` — a wrapping flex row (`<ul>`, markers removed, 12px gap) of inline actions or facts. Use it in a table cell, a card foot or a section head when a few equal-weight links or compact `Button`s sit side by side.

**Rules**

- Each `<li>` holds one `Button` (`href` for links) or one fact; keep the row to two to four items.
- It is not the record action bar — a record's actions live in `RecordBar`; page-level submits use `PrimaryAction`.
- Facts placed here carry text only (bold principal, registration, claimant); state is never shown by colour alone.

**Examples**

```tsx
<ActionList>
  <li><Button href="/Cases/CE-2026-01432">Open case</Button></li>
  <li><Button href="/Triage/assign">Assign to me</Button></li>
</ActionList>
```
