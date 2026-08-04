# Page 14 — Sign in (`/Account/SignIn`)

**No screenshot exists for this page.** DevelopmentOffline auto-authenticates every request, so
`SignInModel.OnGet` (`if (User.Identity?.IsAuthenticated == true) return LocalRedirect(...)`)
redirects before the page can ever render locally. This review is written from source:
`src/Pegasus.Web/Pages/Account/SignIn.cshtml` and `SignIn.cshtml.cs`, plus the shared layout and
`Program.cs` rate-limiter wiring.

Current markup facts: an `auth-panel > panel` card containing eyebrow `Collision Engineers`,
`<h1>Sign in to Pegasus</h1>`, lede *"Use the staff account issued to you. Contact an administrator
if your access has changed."*, a Username field (`autocomplete="username"`, `autofocus`), a Password
field (`autocomplete="current-password"`), and a red `primary-action` button labelled **Sign in**.
Failure adds one model error: *"The username or password is incorrect."* The handler is
`[EnableRateLimiting("StaffSignIn")]` — a fixed window per client IP.

## 1. Aesthetics

- The card itself is clean, but the page renders inside the standard layout, and the layout's nav
  links — the full authenticated menu, all six application sections — are rendered
  **unconditionally**;
  only the user menu is behind `User.Identity?.IsAuthenticated`. An unauthenticated visitor sees
  the whole application menu, every link of which bounces straight back to sign-in. Navigation
  chrome on an unauthenticated screen is pure noise, and it leaks the application's surface map
  before any credential is presented.
- Brand presence is a single 11px uppercase eyebrow. Sign-in is the one screen where the mark
  earns real estate; here it is indistinguishable from a section kicker.
- The lede violates presentation rule §4.1 (no ledes) and narrates policy at a reader who has done
  nothing yet. "Contact an administrator if your access has changed" is failure-state guidance
  shown in the success path.

## 2. Practicality

- The single generic failure message — *"The username or password is incorrect."* — is correct
  practice: disabled and unknown accounts return the identical sentence, so accounts cannot be
  enumerated. Keep the sentence; design its presentation.
- Empty-submit renders the DataAnnotations defaults: **"The UserName field is required."** —
  `UserName` is a code identifier reaching the operator as copy (rule §4.3).
- **The rate-limited state is undesigned.** When the fixed window is exhausted the app returns a
  bare HTTP 429 with a `Retry-After: 60` header and no body (`Program.cs`
  `options.RejectionStatusCode = Status429TooManyRequests`; `OnRejected` writes a security event
  and headers only). Mid sign-in, the operator gets a raw browser error page with no wording and
  no way back — the exact failure family §4.6 requires to be designed.
- No password-reveal affordance; acceptable for a staff app, but worth deciding deliberately.
  `autofocus` on Username and correct `autocomplete` attributes are already right.

## 3. Performance / design / good practice

- Zero JavaScript, one POST, server-rendered — the right shape for this page; nothing to trim.
- Security events are written on every denial with a reason code, and correctly never surface in
  the UI.
- `ReturnUrl` is guarded by `Url.IsLocalUrl` — good.
- A successful sign-in with `MustChangePassword` redirects to the password page, but that
  destination gives no context for the forced stop (reviewed as page 15).
- The one structural fault is inherited, not local: there is no unauthenticated layout. Sign-in,
  and the signed-out confirmation proposed for page 16, need a shared navless auth shell —
  centered card on paper — rather than the application chrome.
