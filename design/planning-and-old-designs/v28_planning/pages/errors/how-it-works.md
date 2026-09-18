Read from the live source on 18 September 2026.

## What this page does not show

- `Error.cshtml`'s support reference is a request id shown only when
  `Model.ShowRequestId` — the mockup always shows it (a synthetic GUID) for
  capture completeness; live it can be absent.
- `StatusCode.cshtml` discloses nothing beyond what the HTTP status already
  told the browser (its own doc comment on the PageModel).

## Source table

| File | Owns |
| --- | --- |
| `Pages/Error.cshtml` | The generic fault card: statement, optional "Try again" (a GET replay of the return path, never a POST replay), Return to Work Centre, optional support reference with Copy |
| `Pages/StatusCode.cshtml` | `/status/{code}`, one heading+explanation pair per status code |
| `Pages/StatusCode.cshtml.cs` | The code→heading/explanation switch: 404, 413, 429 (sign-in surface vs general), 403, default |

## Behaviours

### Status 429 reads differently on the sign-in surface

`StatusCodeModel.OnGet` checks `IsStaffSignInSurface()`: a 429 reached while
signing in reads "Too many sign-in attempts / Wait a minute, then try
again."; the same code elsewhere reads the generic "Too many requests" pair.
Both are captured as separate strip states (`status429` here is the sign-in
variant; the generic 429 was not additionally captured as its wording only
drops "sign-in attempts" for "requests" — noted here rather than as a
seventh near-duplicate screenshot).

### This page is anonymous and designed, not a browser default

The PageModel's remarks explicitly record that before this page existed, an
unknown record URL, an oversized staff upload and a rate-limited sign-in all
returned the browser's own error page — `StatusCode.cshtml` is the designed
replacement, `[AllowAnonymous]` so a signed-out operator can still read it
for a stale bookmark or failed request.

## Things the FRD does not settle

- Nothing found while reading these two pages in isolation.
