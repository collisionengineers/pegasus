---
category: Metrics
---

An administration workspace entry (`.admin-card`, a `<section>`): a Lucide icon in a 34px square, the workspace title as an `<h2>` link, and an optional one-line muted description. The link's pseudo-element covers the whole card, so the card is the pointer target while there is exactly one accessible link; the left rail turns red on hover. Use it inside `AdminWorkspaces` and nowhere else.

**Rules**

- `title` is the workspace name in sentence case (`Staff accounts`, `Roles`); `href` is its landing page.
- The description is one plain sentence saying what an administrator does there — business language, no mechanics.
- Icons come from the Pegasus sprite: `user` for staff, `shield` for roles, `file-text` for principals, `filter` for organisations, `lock` for access.
- No counts, chips or buttons in a card; a workspace entry is not a queue card.

**Examples**

```tsx
<AdminCard icon="user" title="Staff accounts" href="/Admin/Staff">
  Enable, disable and reset staff sign-in.
</AdminCard>

<AdminCard icon="shield" title="Roles" href="/Admin/Roles" />
```
