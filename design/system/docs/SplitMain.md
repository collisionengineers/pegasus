---
category: Layout
---

`.split-main` — the list leads at `2fr`, the form that adds to it follows at a minimum of 300px, with a 16px gap; it collapses to one column under 1280px. An even split starves a multi-column table, so the list keeps twice the room. Use it on administration screens (approved mailboxes, principals, staff) where a table and its "add" form share the screen.

**Rules**

- Exactly two children, list first: a `Panel` (holding a `TableWrap` + `DataTable` or a `PlainList`) then a `FormPanel`.
- The form is for adding to the list beside it; editing an existing row happens in that row or on its own screen.
- Do not use it for two peer panels — that is `DashboardGrid`.

**Examples**

```tsx
<SplitMain>
  <Panel>
    <SectionLabel>Approved mailboxes</SectionLabel>
    <TableWrap>…</TableWrap>
  </Panel>
  <FormPanel title="Add a mailbox" form={{ method: 'post' }}>
    …
  </FormPanel>
</SplitMain>
```
