---
category: Overlay
---

`.reason-dialog-backdrop` > `.reason-dialog` — the modal that collects a business reason before a consequential action: a title with the warning glyph, the consequence as an amber `.notice`, a required Reason textarea with its hint, then Cancel (`SecondaryAction`) and Confirm (`PrimaryAction`) right-aligned. It renders `role="dialog" aria-modal` and is `hidden` when `open` is false. `inline` drops the fixed scrim so it can sit in flow for previews or embedded confirmations.

**Rules**

- Title is the question in business words (`Close this case as Created in error?`); `consequence` is one sentence naming what cannot be undone.
- `confirmLabel` names the action (`Close case`, `Reopen case`), never a bare `OK`; Cancel always remains.
- The reason is required and never pre-filled; extra fields go in `children` above the button row.
- The dialog manages focus (initial focus, containment, Escape, return to the invoker) — do not stack a second dialog on top of it.

**Examples**

```tsx
<ReasonDialog
  open={closing}
  id="close-in-error"
  title="Close this case as Created in error?"
  consequence="The case closes and its reference is never reused. Name the replacement case in the reason."
  confirmLabel="Close case"
  onCancel={() => setClosing(false)}
  onConfirm={(reason) => closeCase(reason)}
/>
```
