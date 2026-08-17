---
category: Status
---

`.blocker-list` — the readiness rail's `<ul>`: an unstyled grid with the 8px gap holding one `Blocker` per unmet requirement. It sits beside or above the action it gates (typically the record-level commitment such as Export or Complete) so the operator can see at a glance what still stands between the record and that action.

**Rules**

- Children are `Blocker`s only; one requirement per item, each naming its resolution.
- Order the list the way the operator will fix things (the record's tab order), not by severity.
- When nothing is unmet, render nothing here and enable the action — do not show an empty list or a green "all clear" (green is for confirmed completion only).
- Pair it with the disabled action's `condition` so the rail and the tooltip tell the same story.

**Examples**

```tsx
<BlockerList>
  <Blocker title="Vehicle registration">Enter the registration on the Vehicle tab.</Blocker>
  <Blocker title="Accident date">Confirm the date from the instruction e-mail.</Blocker>
  <Blocker title="Pre-accident value">Record the value or accept the suggested £14,250.</Blocker>
</BlockerList>
```
