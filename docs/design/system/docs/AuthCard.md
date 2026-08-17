---
category: Auth
---

`.auth-card` — the single centred card family for sign in, signed out, password change, access denied, error and not found: a 26rem bordered panel with soft shadow, the small red `COLLISION ENGINEERS` mark, an `<h1>` title, then the body (a paragraph, a form with inputs nested inside labels, or `AuthCardActions`) and an optional `foot` under a hairline. `wide` widens it to 34rem for forms with more fields; `fault` adds the red left rail and `role="alert"`; `done` renders the title with a green tick.

**Rules**

- One statement of the situation: the title says it, one paragraph explains it, the actions offer the way on — never a heading, a tinted panel and a second heading saying the same thing.
- `done` is the only place green appears here and only for a completed action (`You are signed out`); it is a one-time state, never a bookmarkable page.
- Forms nest the `Input` inside its `<label>` and put a single `.field-hint` beside the field it governs; the submit is one full-width `PrimaryAction`.
- `fault` cards (error, not found, access denied) put the primary way forward first in `AuthCardActions` and the support reference in `foot` as `Support reference` + `SupportReference`.
- Copy is business language: no request pipeline, exception or deployment terms.

**Examples**

```tsx
<AuthCard title="Sign in to Pegasus">
  <form method="post">
    <label>Username<Input name="UserName" autoComplete="username" /></label>
    <label>Password<Input name="Password" type="password" autoComplete="current-password" /></label>
    <PrimaryAction>Sign in</PrimaryAction>
  </form>
</AuthCard>

<AuthCard title="We could not complete that request" fault
  foot={<>Support reference <SupportReference reference={requestId} /></>}>
  <p>What you submitted may not have been saved. Try again, and if it keeps failing, tell your administrator the reference below.</p>
  <AuthCardActions>
    <PrimaryAction href={returnPath}>Try again</PrimaryAction>
    <SecondaryAction href="/">Return to Dashboard</SecondaryAction>
  </AuthCardActions>
</AuthCard>
```
