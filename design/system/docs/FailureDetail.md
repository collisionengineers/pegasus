---
category: Status
---

`.failure-detail` — the red-railed detail block rendered directly under a failed action: a grid of short lines on the red tint, with a 3px Collision-red left rail. It says what happened and what the operator can do next, next to the control that failed, rather than in a page-level card.

**Rules**

- Lead with the outcome in `<strong>` (`Nothing was sent.`), then one line of what to do (`Try again, or ask the office…`).
- Business language only: no error codes, adapter, queue or service names; a time the operator can quote is fine.
- Place it immediately below the action it explains; page-wide failures use `StatusCard variant="error"`.
- Do not use red tint for anything that is not a failure.

**Examples**

```tsx
<FailureDetail>
  <strong>Nothing was sent.</strong>
  <span>The case is unchanged. Try again, or ask the office to check the mailbox connection.</span>
</FailureDetail>
```
