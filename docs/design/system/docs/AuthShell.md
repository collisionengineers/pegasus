---
category: Auth
---

`.auth-shell` — the navless, centred, full-height (min-height 100vh) paper ground for sign-in and status cards. It renders `<main id="main-content" tabIndex={-1}>` so a skip link lands on the content, and centres one `AuthCard` in a grid. Use it as the layout for sign in, signed out, forced password change, access denied, error and not found — never for a screen that has application navigation.

**Rules**

- Exactly one `AuthCard` inside; the shell has no heading, lede or footer of its own.
- No `AppNav`: the shell exists precisely because these screens must not offer navigation the operator cannot use.
- The paper background comes from the page body; the shell only supplies the centring and the vertical padding.

**Examples**

```tsx
<AuthShell>
  <AuthCard title="Sign in to Pegasus">
    <form method="post">
      <label>Username<Input name="UserName" autoComplete="username" /></label>
      <label>Password<Input name="Password" type="password" autoComplete="current-password" /></label>
      <PrimaryAction>Sign in</PrimaryAction>
    </form>
  </AuthCard>
</AuthShell>
```
