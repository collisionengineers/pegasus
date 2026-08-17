---
category: Shell
---

`.crumb` — the one-line breadcrumb above a record container: parent links in charcoal, separated by ` / `, and the current item as plain muted text (`Cases / CE-2026-01432`). It is a `<nav aria-label="Breadcrumb">`. Use it as the only thing above a `Record`; a record screen carries no page heading or lede.

**Rules**

- Parents are places the operator can go back to (a nav route, a queue); the current item is the record's reference or name and is not a link.
- Keep it to one line and at most two parents; a deeper trail means the screen is in the wrong place.
- The current item repeats the reference shown in the `RecordHead` — that is intentional.
- Do not use it for tab or section navigation; that is `Tabs`, `Subtabs` or `SectionTabs`.

**Examples**

```tsx
<Crumb parents={[{ label: 'Cases', href: '/Cases' }]} current="CE-2026-01432" />

<Crumb
  parents={[{ label: 'Queues', href: '/Triage' }, { label: 'Not ready', href: '/Triage?stage=not-ready' }]}
  current="CE-2026-01507"
/>
```
