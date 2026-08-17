---
category: Actions
---

The `.secondary-action` page-level hairline companion to `PrimaryAction` — Cancel, Back, or the alternative path. Same size and shape as the primary so the pair reads as one row; charcoal text on the panel ground with a hairline border. Defaults to `type="submit"`, so pass `type="button"` for Cancel, or `href` to render it as a link.

**Rules**

- Sits beside a `PrimaryAction` in a `ButtonRow`; it is not a general-purpose button (compact actions are `Button`).
- Give a Cancel `type="button"` so it does not submit the form.
- Optional `icon` before the label; the label stays.
- The `Send to Claude` control extends this class with `.send-action` — use `SendToClaudeButton` for that, never a hand-styled secondary.

**Examples**

```tsx
<ButtonRow>
  <PrimaryAction>Reopen case</PrimaryAction>
  <SecondaryAction type="button">Cancel</SecondaryAction>
</ButtonRow>

<SecondaryAction href="/Cases" icon="arrow-right">Back to Cases</SecondaryAction>
```
