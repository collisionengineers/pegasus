---
category: Layout
---

`.dashboard-grid` — two equal columns of panels with a 12px gap, aligned to the top; it collapses to one column under 1280px. Use it under the dashboard's metric strips, or on any overview screen whose panels are peers, so panels never need bespoke widths.

**Rules**

- Children are `Panel`s (or `QueueCard`s); each names itself with a `SectionLabel`.
- Peers only: if one side leads and the other adds to it, use `SplitMain`; for side-by-side comparison use `ReviewGrid`.
- Do not nest a grid inside a grid or force a third column.

**Examples**

```tsx
<DashboardGrid>
  <Panel>
    <SectionLabel>Active cases</SectionLabel>
    …
  </Panel>
  <Panel>
    <SectionLabel>E-mail activity</SectionLabel>
    …
  </Panel>
</DashboardGrid>
```
