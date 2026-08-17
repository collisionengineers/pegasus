---
category: Forms
---

`.role-form` — the narrow (17rem minimum) in-table administration form: a `ChoiceGroup` of the account's roles, a bold direct-child `<label>` wrapping the required Reason input, and a `ButtonRow` with the `PrimaryAction` (`Save roles`). It posts by default and prevents navigation in previews. Use it in the actions cell of the Roles table, one per account.

**Rules**

- Compose it as `ChoiceGroup` → Reason `<label>` (a direct child, so the `.role-form > label` rule styles it) → `ButtonRow` with one `PrimaryAction`.
- Every role change carries a reason: the Reason input is `required` and the label says `Reason`, nothing softer.
- The legend names the account (`Roles for j.patel`); the checked boxes show current roles.
- Keep it inside its row — it never becomes a page-level form or a dialog; removing the final Administrator is refused by the server, so do not hide the box.

**Examples**

```tsx
<RoleForm>
  <ChoiceGroup legend="Roles for j.patel">
    <Choice name="SelectedRoles" value="Administrator">Administrator</Choice>
    <Choice name="SelectedRoles" value="Engineer" defaultChecked>Engineer</Choice>
    <Choice name="SelectedRoles" value="User" defaultChecked>User</Choice>
  </ChoiceGroup>
  <label>
    Reason
    <Input name="Reason" required maxLength={1000} />
  </label>
  <ButtonRow><PrimaryAction>Save roles</PrimaryAction></ButtonRow>
</RoleForm>
```
