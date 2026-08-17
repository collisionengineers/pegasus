---
category: Forms
---

`<details>` + `.row-confirm` — an action that needs a reason confirms in the row it belongs to instead of a dialog: the `summary` is a compact `.btn` (no disclosure marker); open, it reveals a flex row with the eyebrow-size Reason label, a 12rem-minimum required input, and the dark `.btn--dark` confirm button. Use it in table rows for reasoned actions such as `Withdraw link`.

**Rules**

- `summary` and `confirm` name the same action in the same words (`Withdraw link` / `Withdraw link`); the confirm is the committed step, so it is the dark button.
- Give each row a unique `reasonId`; the reason input is `required` and capped at 500 characters.
- Pass `open` only for previews or when the row is already mid-confirmation after a failed post — the operator opens it themselves otherwise.
- Do not use it for actions without a reason (a plain `Button`), and never for a page-level commitment (that is a `PrimaryAction` in a `FormPanel`).

**Examples**

```tsx
<RowConfirm summary="Withdraw link" reasonId="withdraw-reason-42" confirm="Withdraw link" />

<RowConfirm summary="Withdraw link" reasonId="withdraw-reason-42" confirm="Withdraw link" open />
```
