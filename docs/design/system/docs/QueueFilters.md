---
category: Metrics
---

A row of hairline filter links above a queue list (`.queue-filters`, a `<nav aria-label="Filter">`): each filter is a bordered pill-shaped `<a>` in small bold text, and the active one carries `aria-current="page"`. Use it when a list can be viewed a few ways (`All`, `Awaiting instruction`, `Associated with Case`) and each view is a real URL; it is not a form and not a tab row.

**Rules**

- Three to five filters, each a full page destination; put the broadest (`All`) first and mark exactly one `current`.
- Labels are the settled queue and state words in sentence case; never mechanic terms.
- Pass a specific `aria-label` (`Mail filters`) when the page has more than one filter row.
- Sits directly above the `QueueList` it filters (it carries its own bottom margin); do not combine it with a `Tabs` row for the same list.

**Examples**

```tsx
<QueueFilters
  filters={[
    { label: 'All', href: '/Images', current: true },
    { label: 'Awaiting instruction', href: '/Images?associated=no' },
    { label: 'Associated with Case', href: '/Images?associated=yes' },
  ]}
/>
```
