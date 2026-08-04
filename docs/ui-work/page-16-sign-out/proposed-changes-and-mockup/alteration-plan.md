# Page 16 — Sign out — alteration plan

## Review summary

The sign-out page is the only unstyled screen in the application — a bare `<h1>` and a raw browser
button — and it is also dead markup: `OnGet` redirects to the home page and the nav already posts
sign-out directly. What is actually missing is any confirmation that the session ended.
**Recommendation: drop the interstitial entirely.** The nav keeps its direct POST; the dead page
markup goes; a designed signed-out confirmation card (in the sign-in card family) becomes the
post-sign-out destination.

## Changes

1. **Interstitial removed**: the unstyled `Sign out` page markup → deleted. The nav's existing
   `<form method="post">` Sign out button remains the only sign-out control. (Alternative
   considered: styling the interstitial as a "Sign out of Pegasus?" confirm/cancel card — rejected
   because no current flow reaches it, and adding a confirm step would change nav behaviour to
   solve a problem nobody has reported. The wireframe records the alternative for completeness.)
2. **Post-sign-out destination**: redirect to the bare sign-in form → redirect to a signed-out
   confirmation state rendered in the navless auth shell: mark, h1 **"You are signed out"**, and a
   primary **Sign in** action leading to `/Account/SignIn`.
3. **Confirmation semantics**: the signed-out state uses the green confirmed-completion role as a
   small check indicator beside the heading — a completed action, the one place green is earned on
   the auth family.
4. **Security shape unchanged**: sign-out stays a POST with antiforgery; GET on any sign-out URL
   still redirects without ending the session.

## Dependencies

- Navless auth shell shared with pages 14 and 15.
- Routing: the POST handler's redirect target changes from `/Account/SignIn` to the signed-out
  confirmation (either a distinct page or a one-time state of the sign-in page — one-time state
  recommended so an old bookmark cannot show a false "signed out" claim).
- Removal of the dead `SignOut.cshtml` markup (handler file remains).

## Open questions

- Distinct confirmation page vs one-time state on the sign-in page (plan recommends the one-time
  state; a bookmarked confirmation page would assert something that did not just happen).
- Is a confirm-before-sign-out step wanted anywhere (e.g. mid-form protection)? Nothing in the
  current product holds unsaved state long enough to justify it; revisit if long forms arrive.
