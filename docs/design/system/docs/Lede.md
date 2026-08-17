---
category: Layout
---

A muted one-line intro: `.lede` is a `<p>` in the muted colour, capped at 68ch, with a small top margin. Design rule: screens carry no lede or subtitle — the record head, tabs and bar say what the screen is. Use `Lede` only beside a consequential control, where one sentence states the consequence the operator is about to commit to (closing a case as created in error, reopening, sending).

**Rules**

- Never under a page or record heading, and never as decoration; if the sentence is not about a consequence, delete it.
- One sentence in business language, stating what will happen (`The reference will not be reused and this case will not reopen.`).
- Place it directly between the heading and the control it qualifies.
- A condition on a disabled action belongs on the `Button` (`condition`), not in a lede.

**Examples**

```tsx
<h2>Close as created in error</h2>
<Lede>The reference CE-2026-01432 will not be reused and this case will not reopen.</Lede>
<Button variant="primary">Close case</Button>
```
