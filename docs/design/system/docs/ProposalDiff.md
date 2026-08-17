---
category: Record
---

The recorded value beside a proposed value at equal weight (`.proposal-diff`): a hairline-framed two-column grid, each side an eyebrow `<h3>` title and the value in a `<p>`. Neither side is styled as the default outcome. Use it on the suggestions screen so the operator compares what is on the case now with what is being suggested before choosing.

**Rules**

- Pass `title` on both sides in operator words — `On the case now` / `Claude suggests` — rather than the fallbacks `Recorded`/`Proposed`.
- A missing side is the em-dash idiom (`<p className="tabular"><span aria-hidden="true">—</span><span className="vh">No recorded value</span></p>`), never an empty panel.
- Values are plain business values in a `<p>` (a string is wrapped for you; pass a node for tabular numerals or multi-line text). No colour, no chip, no tick on either side.
- Below a 900px viewport the two sides stack; keep it inside a panel or record body, not in a table cell.

**Examples**

```tsx
<ProposalDiff
  recorded={{ title: 'On the case now', children: '£13,400' }}
  proposed={{ title: 'Claude suggests', children: '£14,250' }}
/>

<ProposalDiff
  recorded={{
    title: 'On the case now',
    children: <p className="tabular"><span aria-hidden="true">—</span><span className="vh">No recorded value</span></p>,
  }}
  proposed={{ title: 'Claude suggests', children: <p className="tabular">6 Aug 2026</p> }}
/>
```
