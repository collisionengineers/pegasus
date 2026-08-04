# UI shell and design system — task plan

Branch `task/ui-shell-and-design-system`. First of the seven page PRs in the UI
implementation programme (`NOW.md`). It builds the things every other page PR
needs and owns the screens that are not a place in the application.

Specification: `docs/ui-work/ui-standards-and-review.md` (presentation rules),
the page 14–18 alteration plans, and the refreshed mockups.

## What this PR owns

1. **The refreshed presentation system** in `src/Pegasus.Web/wwwroot/css/site.css`.
   Tokens retuned to the declared divergences (paper `#F7F6F4`, 6px radius with a
   very low shadow, a real type scale with tabular numerals, 10%-tinted chips)
   and the density figures applied throughout: 4px base with 8/12/16 steps,
   32px table rows, 28px fact rows, 13.5px body, 34px controls. New component
   vocabulary for the record container, the action bar, tabs and sub-tabs,
   provenance icons, the condition tooltip, fact columns, the auth card family
   and the compact refresh.
2. **The new navigation** in `_Layout`: Dashboard · Inbox · Queues · Cases ·
   Administration. `Search` keeps its item until the Cases PR lands the
   redirect, because an unreachable page is worse than one item too many; the
   `Upload` item arrives with the Upload PR.
3. **Two more layouts.** `_LayoutAuth` for the screens that are not a place in
   the application, and `_LayoutExternal` for the one screen a third party
   sees.
4. **Designed status-code pages** (`/status/{code}`), scoped away from the
   machine surfaces.
5. **The five shell screens**: sign in, change password, sign out, access
   denied, error.
6. **`OperatorLabels`**: the single place a persisted code becomes words.
7. **`wwwroot/js/site.js`**: the progressive enhancements, as a file rather
   than inline blocks the deployed CSP discards.
8. **The disposable verification Administrator** (`claudeuiverification`).

## Defects closed

| Ref | Finding | How |
|---|---|---|
| M4 | Unknown record URLs return raw browser 404s | Status-code pages with a worded not-found card |
| M5 (partly) | Raw enum values as user-facing text | `OperatorLabels`; `_StatusChip` no longer mis-tones a PascalCase compound |
| M6 | Freshness banner can label a UTC time "London" | The label follows the resolved zone; the banner is now a corner element and the redundant "Current" badge is gone |
| M9b | External upload and sign-in show internal staff navigation | `_LayoutExternal` and `_LayoutAuth` |
| M9c | External dead-link outcomes return raw browser 404s | Status-code page, with external wording and no internal link |
| M9d | Sign-in rate limiting returns a bodyless HTTP 429 | Status-code page: "Too many sign-in attempts" |
| M9e | Framework validation strings reach operators verbatim | Explicit `ErrorMessage`/`[Display]`; the password-change outcomes are told apart |
| M10 | Sign-out page is unstyled | The interstitial is gone; the confirmation is a one-time state of the sign-in card |
| Minor | Error page states one failure three times, trace ID as hero | One card, "Support reference" demoted to the foot with a copy button |
| Minor | Sign-out is dead markup | Route kept (the nav posts to it), markup removed |
| Minor | Access review shows UTC | Not this PR — Administration owns it |

`M9e` needed no Core change: `StaffPasswordChangeError` already distinguishes
`CurrentPasswordInvalid`, `PasswordUnchanged` and `PasswordRejected`. The page
was collapsing all three into one sentence. The defect register's dependency
note ("needs the error split in Core") was wrong about the cause.

## Verification

- `dotnet build --configuration Release`
- `dotnet test` — architecture, Core, integration including the Playwright
  accessibility sweep at 1280×800, 1024×768 and forced colours
- New `ShellAndStatusPageWebTests`: the navigation vocabulary and the absence
  of an inert item; an unknown record URL rendering the designed page; the
  sign-out confirmation redirect; the navless screens carrying no staff
  navigation; the machine surfaces not being hijacked by status-code pages
- `/status/404` added to the accessibility route sweep

## Deliberately not in this PR

- Page content and per-page structure: each page PR owns its own screen.
- The Search retirement and redirect (Cases PR).
- The `Upload` nav item (Upload PR).
- The `DraftReady`/acceptance-gate removal (Inbox PR).
