---
category: Shell
---

`.section-tabs` — page-level section navigation over a hairline (assessment sections, Operations areas). Each item is a 44px link; the current one carries `aria-current="page"` and mirrors the shell's active-route treatment: red, bold, with a 2px underline sitting on the rule. Use it directly under a `PageHeading` when a screen is split into sibling sections that each have their own route.

**Rules**

- Sections are alternatives, not steps: do not number them or imply an order.
- Exactly one current item; the state is carried by weight and underline as well as colour.
- Labels are business nouns (`Vehicle`, `Damage`, `Valuation`) — no counts; counts belong on `Tabs` and `Subtabs`.
- Inside a record container use `Tabs`; for a nested level under a tab use `Subtabs`.

**Examples**

```tsx
<SectionTabs
  label="Assessment sections"
  tabs={[
    { label: 'Vehicle', href: '?section=vehicle', current: true },
    { label: 'Damage', href: '?section=damage' },
    { label: 'Valuation', href: '?section=valuation' },
  ]}
/>
```
