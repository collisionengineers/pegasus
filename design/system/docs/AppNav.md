---
category: Shell
---

The white top bar of the staff shell (`.app-nav` > `.nav-inner`): the brand link (Collision Engineers logo at 104×32 plus the product name behind a hairline), the primary routes as `.nav-link`s, and the user group (`.user-menu`: name, Change password, Sign out) behind a hairline rule. The current route carries `aria-current="page"` and is red, bold and underlined — never colour alone. Use it once per authenticated screen; pass `brandOnly` for the public upload shell, which shows the logo and nothing else.

**Rules**

- Pass routes in the settled order: Dashboard, Inbox, Upload, Queues, Cases, Operations, then Administration for administrators only. A capability that is not composed in a deployment is absent — never a disabled item or placeholder.
- Exactly one item is `current`; it is the route the operator is on, not a hover or focus state.
- `userName` renders the user group with Change password and Sign out; omit it and the group collapses to a single Sign in link.
- Never rename the routes in operator copy (`Triage` is not a nav item; the mail surface is `Inbox`, never "intake").
- Under 1024px the bar stacks (brand, links, then the user group under a hairline) — do not add a hamburger or hide routes.

**Examples**

```tsx
<AppNav
  items={[
    { label: 'Dashboard', href: '/', current: true },
    { label: 'Inbox', href: '/Mail' },
    { label: 'Upload', href: '/Upload' },
    { label: 'Queues', href: '/Triage' },
    { label: 'Cases', href: '/Cases' },
    { label: 'Operations', href: '/Operations' },
  ]}
  userName="alex.mercer@collisionengineers.co.uk"
  onSignOut={signOut}
/>

<AppNav items={[]} brandOnly />
```
