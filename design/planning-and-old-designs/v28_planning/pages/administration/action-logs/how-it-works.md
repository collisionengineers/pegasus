Read from the live source on 18 September 2026.

## What this page does not show

Everything — the route has no page body left to show.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/ActionLogs.cshtml` | Empty (`@page` + `@model` only) |
| `Pages/Administration/ActionLogs.cshtml.cs` | `OnGet()` — the permanent redirect, query string preserved |

## Behaviours

### A closed redirect, not a page

The class doc comment states it plainly: "The former Action logs address.
Action logs are the first tab of Administration › Logs (13 September); the
old route redirects there with its filters intact." `[Authorize(Policy =
StaffRoleNames.Administrator)]` still guards it, so an unauthorised request
is refused before the redirect runs, but an authorised one always lands on
`/Administration/Logs` with its original query string appended — filters
survive the hop. See [Logs](../logs/) for the tab this now is.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
