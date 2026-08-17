---
category: Shell
---

The authenticated page frame: a skip link to `#main-content`, the `nav` slot (an `AppNav`), `.app-shell` > `<main>` at 1440px max width on the paper ground, and `.footer` ("Pegasus · Collision Engineers case management" by default). Every staff screen renders inside `main`, starting with a `PageHeading` and then panels, a record container, or a queue. Use it as the outermost element of a screen composition; pass `footer={null}` for navless surfaces (sign in, denied, error) and a brand-only nav plus a company footer for the one screen a third party sees.

**Rules**

- One `PageHeading` at the top of `main`, no lede or subtitle; then the screen's content on the paper ground.
- The external shell states the company, never the product: `nav={<AppNav items={[]} brandOnly />}` and `footer="Collision Engineers"`.
- Keep `main` as the only landmark for content; do not nest a second `<main>` or wrap children in another max-width container.
- The skip link is part of the frame and stays first in the DOM; do not remove it.

**Examples**

```tsx
<AppShell nav={<AppNav items={routes} userName="Alex Mercer" onSignOut={signOut} />}>
  <PageHeading title="Queues" refresh={<Refresh updatedAt="14 Aug 09:32" />} />
  <Panel>
    <SectionLabel>Not ready</SectionLabel>
    <p>7 cases are waiting for information from the instructing principal.</p>
  </Panel>
</AppShell>

<AppShell nav={<AppNav items={[]} brandOnly />} footer="Collision Engineers">
  <PageHeading title="Upload images for LM19 KXR" />
  …
</AppShell>
```
