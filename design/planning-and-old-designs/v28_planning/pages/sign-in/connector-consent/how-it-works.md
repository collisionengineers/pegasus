Read from the live source on 18 September 2026.

## What this page does not show

- No branding for the connector beyond its origin and display name; no icon
  or logo slot.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Connect/Authorize.cshtml` | The OAuth-style consent card: connector identity, requested scopes, Authorise/Refuse |
| `Pages/Connect/Authorize.cshtml.cs` | `ClientDisplayName`, `ConnectorOrigin`, `RequestedScopes`, `RequestParameters`, the `Accept`/`Deny` handlers |

## Behaviours

### Never a staff-rights grant

The fixed notice above the request states the connector "never receives
staff rights" and every action runs as the named Automation Actor "through
the approved tool inventory" — this is the one place that vocabulary
(CONTEXT.md's Automation Actor) reaches an operator-facing screen, worded
generically rather than naming the actor's internal identifier.

### Zero requested scopes is a real, distinct state

When `RequestedScopes.Count == 0`, the page states plainly that the
connector "would be unable to call any tool" rather than showing an empty
list — captured here as the `acc-scopes=none` state.

### Wide card

This is the one navless screen that opts into `auth-card--wide consent-card`
(`ViewData["AuthCardClass"]`) to fit the scope list; every other navless
screen uses the plain `auth-card`.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
