---
category: Actions
---

The compact `.btn` action-bar button used in record bars, table rows and filter bars. `default` is the hairline button; `dark` (charcoal) is the bar's committed action; `primary` spends Collision red and is reserved for the one primary action on a screen; `light` sits on the dark record band. Page-level form submits use `PrimaryAction`/`SecondaryAction` instead.

**Rules**

- Pass `href` to render the same shape as a link; a disabled link gets `.is-disabled` and `aria-disabled` rather than losing its label.
- A disabled action states its condition rather than disappearing: give it `condition="Available in Review"` and it is wrapped in `.gated` so the condition appears as a tooltip on hover and keyboard focus.
- `icon` draws a Lucide glyph before the label at .875rem; `iconOnly` needs an `aria-label`.
- Use `light` only on the dark band (`RecordHead`); on the paper ground it is invisible.
- One `primary` per screen at most; red is otherwise reserved for active navigation, focus and urgent emphasis.

**Examples**

```tsx
<RecordBar end={<Button variant="dark" href="/Cases/CE-2026-01432/Export" disabled condition="Available in Review">Export</Button>}>
  <Button>Actions</Button>
  <Button icon="upload">Upload evidence</Button>
  <Button icon="refresh-cw" iconOnly aria-label="Refresh" />
</RecordBar>
```
