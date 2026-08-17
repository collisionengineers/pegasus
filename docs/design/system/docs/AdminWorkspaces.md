---
category: Metrics
---

The Administration landing grid (`.admin-workspaces`): an auto-fit grid, minimum 300px per card, of `AdminCard`s — one per administration workspace (Staff accounts, Roles, Principals, Organisations). Use it only on the administration entry screen; it is a directory of workspaces, not a dashboard, so it carries no counts.

**Rules**

- Children are `AdminCard`s only; each names one workspace and links to it.
- Three or four cards; order them by how often an administrator visits, not alphabetically.
- No page lede or intro paragraph above the grid — the card descriptions explain each workspace.
- Do not mix in `QueueCard`s or metrics; a workspace directory is a different surface from a queue overview.

**Examples**

```tsx
<AdminWorkspaces>
  <AdminCard icon="user" title="Staff accounts" href="/Admin/Staff">Enable, disable and reset staff sign-in.</AdminCard>
  <AdminCard icon="shield" title="Roles" href="/Admin/Roles">Assign the roles that decide what each person can do.</AdminCard>
  <AdminCard icon="file-text" title="Principals" href="/Admin/Principals">Instructing insurers and their case reference formats.</AdminCard>
  <AdminCard icon="filter" title="Organisations" href="/Admin/Organisations">Repairers, salvage agents and other parties on a case.</AdminCard>
</AdminWorkspaces>
```
