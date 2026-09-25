# Three initial sign-in designs

Temporary design review artifact, requested on 25 September 2026. Open the
[visual comparison](pegasus_signin_designs_v30.html), then explore each page
at full size. These are proposals, not approved design authority or
application evidence.

## The proposals

| Design | Visual approach | Strength | Tradeoff |
| --- | --- | --- | --- |
| [A — Quiet focus](pegasus_signin_a_v30.html) | White canvas, compact horizontal brand, open form without a card | The calmest and simplest entrance; a single left edge ties brand, heading and fields together | Moves furthest from the current dark frame |
| [B — Brand split](pegasus_signin_b_v30.html) | Charcoal identity panel and white form panel, aligned around a shared centre | Strongest brand presence with a clear, bounded form | Uses more screen area; becomes a stacked layout in narrower windows |
| [C — Charcoal frame](pegasus_signin_c_v30.html) | Dark surround, white card, centred brand and light divider | Most familiar; refines the existing card's balance and hierarchy | The most conservative visual change |

**Recommendation: B.** The approved Pegasus mark has room to identify the
application, while the white form stays concise. Its charcoal and white
surfaces connect naturally to the application shell. A is preferable if the
priority is visual quiet; C is preferable if familiarity matters most.

## What improves

The current 96px mark sits beside a relatively small wordmark, followed by
“Sign in to Pegasus” and closely packed fields. The proposals give identity
and task distinct places, reduce the heading to “Sign in”, and align the
form around a consistent 20px field gap. A uses a smaller horizontal mark;
B and C give the existing 96px mark a vertical composition.

Each alternative retains Inter Variable, the approved Pegasus image,
Collision red, 36px controls, 3–4px radii and the existing field labels.
The company caption identifies Collision Engineers. All assets are embedded
in each HTML file; it can be opened without a server or network.

| Geometry | A | B | C |
| --- | --- | --- | --- |
| Form width | 380px | 360px | 360px within a 440px card |
| Mark at desktop widths | 64px | 96px | 96px |
| Heading | 23px | 23px | 23px |
| Controls | 36px | 36px | 36px |
| Primary layout | Open centred stack | 42% identity / 58% form | Centred card |
| Narrower windows | Same stack, fluid width | Brand strip above the form below 980px | Card fits the window; reduced inset below 480px |

These are deliberate sign-in layout departures under the current request.
They do not propose a new application palette, font, navigation system or
authentication policy.

## Interaction and coverage

Read on 25 September 2026 from `origin/dev` snapshot `32dabfc59`:
`Account/SignIn.cshtml`, its PageModel, `Shared/_LayoutAuth.cshtml`,
`wwwroot/css/site.css` and the approved font, mark and Lucide sprite.
The earlier [baseline preview](pegasus_signin_v30.html) (`?layer=baseline`)
remains available beside these alternatives.

| Current element or behaviour | Treatment in all three alternatives |
| --- | --- |
| Pegasus mark and name | Retained; composition varies by design |
| “Sign in to Pegasus” | Proposed shorter “Sign in”; existing open item I |
| Username and Password | Same labels; associated labels, required feedback and autocomplete semantics |
| Initial username | Empty, matching the PageModel; the old mockup's prefilled `alex` is not carried into the initial state |
| Hidden ReturnUrl | Retained as a fixture; no redirect or authentication is simulated |
| Sign in | One red full-width primary button; Enter submits the demo form |
| Required fields | Existing exact messages: “Enter your username.” and “Enter your password.” |
| Incorrect credentials | Exact existing generic message; username retained and password cleared |
| Signed out | Existing “You are signed out” heading and green check |
| Password visibility | Working Show / Hide proposal; can be switched off in Mockup controls, existing open item J |
| Focus and errors | Visible keyboard focus; first invalid field receives focus; credential error is an alert and receives focus after a demo attempt |
| Narrower layouts | All fields, labels, states and the primary action remain visible |

The task covers the initial login and its immediate feedback. Forced
password change and access denied remain in the earlier v30 navless-family
preview. Adapting the selected frame to that family is a subsequent design
decision, before implementation.

## How to review

Open the [comparison page](pegasus_signin_designs_v30.html). Its thumbnails
are real captures of these mockups. Each preview has a collapsed **Mockup
controls** strip at the bottom left with A/B/C links, the state selector,
the show-password switch, and Reset. This strip is review tooling, outside
the proposed product UI.

Use sample details. Empty submission shows required-field feedback. A
completed form briefly shows “Signing in…” then deliberately demonstrates
the incorrect-credentials response. No account is contacted, no credentials
are stored, and there is no success or destination-page simulation.

Direct presets on every file:

- `?state=default`: initial empty form.
- `?state=validation`: both required-field messages.
- `?state=error`: generic failure, sample username and empty password.
- `?state=signed-out`: confirmation and an empty form.
- `&reveal=off`: hide the proposed password visibility control.
- `&embed=1`: hide the review strip for an unobstructed preview.

## Review decisions

**H2.** Confirm A, B or C as the preferred initial login composition, or
specify the elements to combine. This extends open item H with three
complete alternatives; no choice is recorded yet.

**I.** Confirm the shorter “Sign in” heading, or retain “Sign in to
Pegasus”. All three previews use the shorter heading.

**J.** Confirm the Show / Hide password control, or omit it. The review
strip demonstrates both versions.

**H3.** Confirm the quiet Collision Engineers caption, or omit it from the
selected composition.

No authentication or FRD behaviour change is proposed. After selection,
the implementation handoff is to scope the accepted styling to the auth
frame, settle its use on the rest of that frame's pages, update the owning
design documentation, and apply the selected markup/CSS through the normal
Razor workflow. Application implementation has not begun.

## Evidence and reproduction

The [self-check](v30-signin-selfcheck.html) exercises all three designs,
the four presets, 1580, 1440, 760 and 390px widths, required-field focus,
password visibility, pending submission, failure and retry availability.
The [runner](check-signin-designs.py) adds actual keyboard traversal and
captures each cited state at 1580×1000, 1440×900 and 760×1000. The
[evidence record](v30-signin-shots/verification.json) records the result.

Reproduce using the workstation's existing Node and Python Playwright:

```powershell
node design/planning-and-old-designs/v30_planning/current/build-signin-designs.mjs
python design/planning-and-old-designs/v30_planning/current/check-signin-designs.py
node design/planning-and-old-designs/v30_planning/current/build-signin-designs.mjs
```

The last command embeds the captured thumbnails in the comparison page.
The original `build.mjs` still owns the earlier five-surface walkthrough;
this small companion build owns only the three new login proposals.

Screenshot numbers and direct links are in the
[sign-in page inventory](../pages/sign-in/README.md#three-designs--25-september-2026).

## Limits

This evidence covers static layouts and local mockup interactions. It does
not validate server authentication, rate limiting, password managers,
real session expiry or live assistive technology. The pending state is a
650ms demonstration. The signed-out query preset is a review convenience,
not a proposal to change the application's one-time confirmation. Inter is
embedded, with the existing system-font fallback if a browser refuses it.
