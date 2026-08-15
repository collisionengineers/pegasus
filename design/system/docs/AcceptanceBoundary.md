---
category: Status
---

`.acceptance-boundary` — an amber left-railed `<section>` with an `<h2>` naming what this surface does not yet prove, and one or more paragraphs explaining the boundary. It keeps a screen honest about its evidence: what its numbers count, what they leave out, and what to check elsewhere before relying on them.

**Rules**

- `title` is required and names the boundary plainly (`What this screen does not prove`, `Read alongside the export`).
- Body copy says what is and is not covered, in business terms; it never describes how the figures are produced.
- One boundary per surface, placed where the operator reads it before the figures.
- It is not a warning about a failed action (`StatusCard variant="error"`) or a consequence beside a control (`Notice`).

**Examples**

```tsx
<AcceptanceBoundary title="What this screen does not prove">
  Figures here are counted from the Inbox and Cases lists. Whether an e-mail was received but never shown is not covered by this page.
</AcceptanceBoundary>
```
