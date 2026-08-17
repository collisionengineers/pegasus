---
category: Forms
---

`fieldset.role-choices` — a hairline-bordered, 6px-radius group of `Choice`s with an uppercase muted legend. Choices wrap in a row by default; `stacked` lays them out in a column. Use it wherever a set of checkboxes or radios belongs together — the roles for one account, a rate class, a completeness checklist.

**Rules**

- The legend names what is being chosen and for whom (`Roles for j.patel`, `Rate class`); it is read before the options by assistive tech, so keep it a noun phrase.
- Row layout for short, few options (three roles); `stacked` for radios or when the option text is longer than a word or two.
- Radios in one group share a `name`; checkboxes share a `name` when they post as one list (`SelectedRoles`).
- Do not put a hint or validation inside the fieldset — place them in the surrounding form beside the group.

**Examples**

```tsx
<ChoiceGroup legend="Roles for j.patel">
  <Choice name="SelectedRoles" value="Administrator">Administrator</Choice>
  <Choice name="SelectedRoles" value="Engineer" defaultChecked>Engineer</Choice>
  <Choice name="SelectedRoles" value="User" defaultChecked>User</Choice>
</ChoiceGroup>

<ChoiceGroup legend="Rate class" stacked>
  <Choice type="radio" name="rateClass" value="standard" defaultChecked>Standard</Choice>
  <Choice type="radio" name="rateClass" value="prestige">Prestige</Choice>
</ChoiceGroup>
```
