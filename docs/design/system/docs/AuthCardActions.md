---
category: Auth
---

`.auth-card__actions` — the stacked, full-width action group inside an `AuthCard`: a grid with the 12px gap where every `PrimaryAction` and `SecondaryAction` stretches across the card. Use it on status cards (error, access denied, not found) that offer a way forward rather than a form.

**Rules**

- Primary first, then the way back: `Try again` above `Return to Dashboard`; never two primaries.
- Actions are links (`href`) to a GET destination — a return path is the page the operator was reading re-requested, never a POST replay.
- Do not use it for a form's submit; a form's `PrimaryAction` already fills the card width on its own.

**Examples**

```tsx
<AuthCard title="You do not have access to that page" fault>
  <p>Your account does not include the role that page needs. Ask your administrator if you think it should.</p>
  <AuthCardActions>
    <PrimaryAction href="/">Return to Dashboard</PrimaryAction>
    <SecondaryAction href="/Account/SignIn">Sign in as someone else</SecondaryAction>
  </AuthCardActions>
</AuthCard>
```
