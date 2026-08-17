---
category: Shell
---

`.page-heading` — the screen's one H1 (optionally under a small uppercase `eyebrow`) above a hairline, with the right side reserved for either the freshness element (`refresh={<Refresh … />}`, which right-aligns itself) or the screen's safe primary action and companions in `.page-heading-actions`. Use it once at the top of every screen that is a place in the application; a screen about a single record uses `Crumb` + `Record` instead and has no page heading.

**Rules**

- No lede or subtitle under the title — screens carry none.
- `refresh` takes a `Refresh`; `actions` takes buttons. A queue or dashboard shows freshness; a list that creates things shows its primary action; rarely both.
- One red control per screen at most: a `PrimaryAction` in the actions slot means no other red button below.
- The eyebrow names the area (`Administration`), not a status — status belongs in chips.
- Do not put filters, tabs or counts inside the heading; `SectionTabs`, `Subtabs` and `FilterBar` follow it.

**Examples**

```tsx
<PageHeading title="Dashboard" refresh={<Refresh updatedAt="14 Aug 09:32" />} />

<PageHeading
  eyebrow="Administration"
  title="Staff accounts"
  actions={<PrimaryAction href="/Administration/Staff/New">Add staff member</PrimaryAction>}
/>
```
