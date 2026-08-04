# Page 16 — Sign out (`/Account/SignOut`)

**No screenshot exists for this page.** DevelopmentOffline auto-authenticates every request: the
nav's sign-out POST is immediately followed by re-authentication, and `SignOutModel.OnGet` redirects
to `/Index`, so the page can never be captured locally. This review is written from source:
`src/Pegasus.Web/Pages/Account/SignOut.cshtml`, `SignOut.cshtml.cs`, and
`Pages/Shared/_Layout.cshtml`.

The page's entire markup, quoted in full:

```html
<h1>Sign out</h1>
<form method="post">
    <button type="submit">Sign out of Pegasus</button>
</form>
```

## 1. Aesthetics

- **This is the only unstyled page in the application** — the single screen off the design system
  (also recorded in `ui-standards-and-review.md` §1.2). No `auth-panel`, no `panel`, no button
  class: a bare `<h1>` and a raw default browser button, rendered at the top-left of the content
  area inside the app chrome. Were it ever shown, it would read as a broken deployment.

## 2. Practicality

- The interstitial is effectively dead markup. `OnGet` is `RedirectToPage("/Index")` — a GET can
  never render it — and the layout's Sign out control (`_Layout.cshtml:57-59`) is already a direct
  `<form method="post" asp-page="/Account/SignOut">` button, so a normal sign-out never touches
  this page's HTML either. The one unstyled screen in the product is also one no user should ever
  see; it survives as a trap for the day a link points at it.
- After the POST the user lands on `/Account/SignIn` with **no confirmation they were signed
  out**. On a shared office machine, "did that work?" is a real question; today the only signal
  is that the sign-in form appeared.
- There is no confirm step before sign-out — a nav misclick ends the session. For a staff app
  with quick sign-back-in this is defensible, but it is a decision to make, not a default to
  inherit.

## 3. Performance / design / good practice

- The security shape is right and must be preserved: sign-out is a POST (antiforgery-protected),
  and the GET redirect means a hostile link cannot end a session. Keep both.
- The page model is four lines; nothing to optimise.
- The genuine design decision is between (a) a styled confirm interstitial and (b) no interstitial
  at all — nav posts directly (as it already does) and the user receives a designed signed-out
  confirmation. Option (b) matches current behaviour, deletes the dead markup, and adds the one
  thing actually missing: proof the session ended. Recommended in the alteration plan.
