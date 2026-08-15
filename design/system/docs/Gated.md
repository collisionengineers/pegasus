---
category: Actions
---

`.gated` — an inline wrapper around a disabled control that shows its unlocking condition (`data-condition`) as a one-line dark tooltip on hover and keyboard focus-within. It is how a disabled action states its condition rather than disappearing. `Button` applies it for you when given `condition`; use `Gated` directly for any other disabled control.

**Rules**

- The child is a real disabled control (`disabled` or `aria-disabled`); the wrapper only adds the condition, it does not disable anything.
- Write the condition as when the action becomes available: `Available in Review`, `Needs a vehicle registration` — one short line, no mechanics.
- The tooltip appears only on hover/focus; the visible disabled state still needs a clear label.
- Do not hide the control instead — removing it says the action is impossible, which is false when the record will offer it later.

**Examples**

```tsx
<Gated condition="Available in Review">
  <Button variant="dark" disabled>Export</Button>
</Gated>

// Equivalent shorthand on Button:
<Button variant="dark" disabled condition="Available in Review">Export</Button>
```
