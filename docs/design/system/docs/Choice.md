---
category: Forms
---

`label.choice` — one checkbox or radio with its text on a single line: 1.125rem control in Collision red (`accent-color`), 8px gap, semibold small text. Use it for a completeness tick, a role, or one option in a `ChoiceGroup`; the whole label is the hit area.

**Rules**

- `type="checkbox"` (default) for independent yes/no items; `type="radio"` with a shared `name` for one-of-many.
- The text is the option in business language (`Instructions complete`, `Engineer`, `Prestige`); it is the accessible name — never leave it empty.
- Group related choices in a `ChoiceGroup` so the legend names what is being chosen; a lone `Choice` is fine for a single confirmation.
- Checked state is shown by the control itself and its text — do not add colour or icons to say "selected".

**Examples**

```tsx
<Choice name="instructionComplete" defaultChecked>Instructions complete</Choice>
<Choice name="imagesComplete">Images complete</Choice>

<Choice type="radio" name="rateClass" value="standard" defaultChecked>Standard</Choice>
<Choice type="radio" name="rateClass" value="prestige">Prestige</Choice>
```
