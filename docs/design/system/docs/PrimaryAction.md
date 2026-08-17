---
category: Actions
---

The `.primary-action` page-level form submit in Collision red. One per screen: red is reserved for the primary action, active navigation, focus and urgent emphasis. Defaults to `type="submit"`; pass `href` to render the same shape as a link.

**Rules**

- Exactly one `PrimaryAction` per screen; its companion is `SecondaryAction`, laid out together in a `ButtonRow`.
- Label the action with the verb the operator is committing to (`Save changes`, `Reopen case`, `Upload evidence`), never `OK` or `Submit`.
- Optional `icon` draws a Lucide glyph before the label; the label is never replaced by an icon.
- Compact actions inside record bars, tables and filter bars are `Button`, not this.

**Examples**

```tsx
<ButtonRow>
  <PrimaryAction>Save changes</PrimaryAction>
  <SecondaryAction type="button">Cancel</SecondaryAction>
</ButtonRow>

<PrimaryAction href="/Cases/CE-2026-01432?tab=review" icon="arrow-right">Continue to Review</PrimaryAction>
```
