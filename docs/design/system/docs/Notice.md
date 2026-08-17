---
category: Status
---

`.notice` — an amber left-railed `<aside>` placed above a form or list carrying one consequence the operator must understand before acting: what will happen, what will not, what cannot be undone. A string child is wrapped in a `<p>`; pass elements for richer copy.

**Rules**

- One consequence per notice, in business language (`retires the reference`, `returns to Review`) — never queue, adapter or deployment mechanics.
- Amber means incomplete/pending or "take care"; it is not an error (that is `StatusCard variant="error"` or `ValidationSummary`) and not decoration.
- Place it directly above the control it qualifies, before the fields, not at the foot of the page.
- The notice states; it does not carry buttons or links to elsewhere.

**Examples**

```tsx
<Notice>Closing this case as Created in error retires the reference CE-2026-01432. It will not be reused.</Notice>

<Notice>
  <p style={{ margin: 0 }}>Reopening needs a reason. The case returns to <b>Review</b> and the previous completion stays in History.</p>
</Notice>
```
