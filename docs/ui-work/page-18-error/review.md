# Page 18 — Error (`/Error`)

Screenshot: `error.png`. Source: `src/Pegasus.Web/Pages/Error.cshtml`. This folder also owns the
**missing not-found page**: unknown record URLs currently return raw browser 404s (recorded in
`ui-standards-and-review.md` §1.2), and a not-found design belongs to this error family.

Current facts: inside the full app layout, a page heading with eyebrow **"PEGASUS"**, h1 **"We
could not complete that request"**, and lede *"No changes should be assumed. Return to Operations
and try the action again."* Below it, a red-tinted `status-card--error` (`role="alert"`) with h2
**"Request failed"**, sentence *"If the problem continues, give the request ID below to the system
administrator."*, then **Request ID:** `00-bf31b4c01d871a90cee3eb32ad0bcdec-4976ac84fe696071-00`
in bold + `<code>`, and a red primary button **"Return to Operations"**.

## 1. Aesthetics

- The page says "failed" three times — h1, the red-tinted alert card, and its own h2 "Request
  failed" — two heading stacks for one fact, against §4.7. The full-width red-washed panel turns
  an apology into an alarm wall.
- The eyebrow kicker "PEGASUS" above the h1 is exactly the kicker §4.7 removes.
- The most visually prominent string on the page is a 55-character W3C trace identifier in bold
  monospace. A raw identifier as the hero content is the sharpest §4.4 violation in the app's
  error family.

## 2. Practicality

- *"No changes should be assumed"* is passive machine voice; the operator's actual question is
  "did my submission save?" — the useful sentence is active: what you submitted may not have been
  saved.
- **"Return to Operations"** names a nav item the IA renames to Dashboard; the copy will be stale
  the day the nav changes. There is also no "try again" affordance despite the lede telling the
  operator to try the action again — the page gives advice it does not implement.
- The "Request ID" is genuinely needed (it is the support correlation handle) but is unlabelled
  for a non-technical reader, un-copyable except by careful manual selection, and presented as if
  the operator should read it rather than pass it on.
- **The 404 gap**: an operator following a dead or mistyped record link gets the browser's
  unstyled 404 — no brand, no wording, no way back. The error page family is incomplete without a
  designed not-found sibling; the raw HTTP 400 from an oversized upload (root doc §1.2) shows the
  same family gap from another direction.

## 3. Performance / design / good practice

- `role="alert"` on the status card is correct and should be kept on the redesigned single card.
- Server-rendered, trivial page model, `ShowRequestId` guard — fine.
- Rendering inside the full layout is risky for this page specifically: `/Error` serves unhandled
  exceptions, the one moment the surrounding shell is least trustworthy. The navless centered
  error card is the safer default.
- Only the exception handler routes here; there is no status-code-pages wiring, which is why 404
  (and 400) fall through to the browser. The fix is one middleware registration plus the designed
  page — small cost, whole-family payoff.
