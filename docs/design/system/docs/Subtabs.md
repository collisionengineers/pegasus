---
category: Record
---

`.subtabs` — pill sub-navigation for a nested level under a tab or heading (mail folders, case sub-states). Each pill is a hairline link with an optional muted count (`.n`); the current pill (`aria-current="page"`) is filled charcoal. An optional `end` slot pushes a control to the right; `sectionGap` adds the section margin above. Pills are used precisely so a nested level never reads as a second tab row.

**Rules**

- Use it one level below `Tabs` or `SectionTabs`, never as the top-level navigation of a screen.
- Labels are the settled state names (`Needs sorting`, `Blocked`, `Not ready`, `Review`, `Held`); counts are real numbers — `0` renders as `0`.
- One current pill; the fill carries the state along with `aria-current`.
- Keep the `end` slot to one compact `Button` (an export or filter), not a form.

**Examples**

```tsx
<Subtabs
  label="Folders"
  tabs={[
    { label: 'Needs sorting', href: '?folder=sorting', count: 3, current: true },
    { label: 'Blocked', href: '?folder=blocked', count: 1 },
  ]}
  end={<Button icon="file-text">Export list</Button>}
/>
```
