---
category: Record
---

`.tabs` — the record container's tab row on the paper ground, between the `RecordBar` and the `RecordBody`. Each tab is a link (`href`, `aria-current="page"` when current) or a button (`onClick`, `aria-selected`) with an optional `.count` pill; the current tab is red with a 2px underline and its pill turns red-tinted. Tabs appear when a record's sections are alternatives; a record whose sections form a reading order gets a body and no tab row.

**Rules**

- Always pass `label` (the accessible name of the set, e.g. `Case sections`).
- Use links when each tab is a route (`?tab=evidence`); use buttons only for sections switched in place.
- Counts are totals that exist; a section without a countable thing gets no pill, and `0` is shown as `0`.
- Exactly one current tab; do not colour tabs by state — the record's state lives in the head chip and accent.

**Examples**

```tsx
<Tabs
  label="Case sections"
  tabs={[
    { label: 'Overview', href: '?tab=overview', current: true },
    { label: 'Evidence', href: '?tab=evidence', count: 7 },
    { label: 'History', href: '?tab=history', count: 12 },
  ]}
/>

<Tabs label="Triage sections" tabs={[
  { label: 'Overview', onClick: () => show('overview') },
  { label: 'Evidence', count: 4, current: true, onClick: () => show('evidence') },
]} />
```
