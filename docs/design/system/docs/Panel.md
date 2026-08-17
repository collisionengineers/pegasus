---
category: Layout
---

`.panel` — a white card on the paper ground: 16px padding, hairline border, 6px radius and the soft shadow, rendered as a `<section>`. It is the unit of a dashboard, an administration list, or any screen that is not about a single record. Head it with a `SectionLabel` (an eyebrow-styled `<h2>`) and put the content directly inside.

**Rules**

- One topic per panel, named by its `SectionLabel`; do not add a second heading level or a lede.
- A screen about one record is one `Record` container, never a vertical stack of sibling panels.
- Compose panels with `DashboardGrid`, `SplitMain` or `ReviewGrid`; do not hand-roll widths.
- Zero results inside a panel are an `EmptyState` line, not an empty box.
- A form section is a `FormPanel` (already a `.panel`), not a `Panel` wrapping a form.

**Examples**

```tsx
<Panel>
  <SectionLabel>Next chase</SectionLabel>
  <p>Repairer images requested on 12 Aug; next chase due 15 Aug.</p>
</Panel>

<Panel>
  <SectionLabel icon="clock">Received</SectionLabel>
  <DetailList items={[{ term: 'Instruction', value: '12 Aug 09:14' }, { term: 'Principal', value: 'Direct Line' }]} />
</Panel>
```
