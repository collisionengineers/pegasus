# Error and status-code family

- **Live source:** `src/Pegasus.Web/Pages/Error.cshtml`, `src/Pegasus.Web/Pages/StatusCode.cshtml`, `src/Pegasus.Web/Pages/Account/AccessDenied.cshtml`
- [**How it works**](how-it-works.md)

Access denied renders inside the ordinary shell, not the navless frame.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Access denied | `/Account/AccessDenied` | [frame](../../current/pegasus_account_shell_v28.html#access-denied) · [page](../../current/states/access-denied.html) | [1580](../../current/v28-shots/s69-access-denied-1580.png) · [1440](../../current/v28-shots/s69-access-denied-1440.png) · [760](../../current/v28-shots/s69-access-denied-760.png) |
| Access denied, User role opening Administration | `/Account/AccessDenied?ReturnUrl=%2FAdministration` | [frame](../../current/pegasus_account_shell_v28.html#access-denied-user-role) · [page](../../current/states/access-denied-user-role.html) | [1580](../../current/v28-shots/s66-access-denied-user-role-1580.png) · [1440](../../current/v28-shots/s66-access-denied-user-role-1440.png) · [760](../../current/v28-shots/s66-access-denied-user-role-760.png) |
| Error | `/Error` | [frame](../../current/pegasus_account_shell_v28.html#error) · [page](../../current/states/error.html) | [1580](../../current/v28-shots/s70-error-1580.png) · [1440](../../current/v28-shots/s70-error-1440.png) · [760](../../current/v28-shots/s70-error-760.png) |
| Status 404 | `/status/404` | [frame](../../current/pegasus_account_shell_v28.html#status-404) · [page](../../current/states/status-404.html) | [1580](../../current/v28-shots/s71-status-404-1580.png) · [1440](../../current/v28-shots/s71-status-404-1440.png) · [760](../../current/v28-shots/s71-status-404-760.png) |
| Status 403 | `/status/403` | [frame](../../current/pegasus_account_shell_v28.html#status-403) · [page](../../current/states/status-403.html) | [1580](../../current/v28-shots/s72-status-403-1580.png) · [1440](../../current/v28-shots/s72-status-403-1440.png) · [760](../../current/v28-shots/s72-status-403-760.png) |
| Status 429 | `/status/429` | [frame](../../current/pegasus_account_shell_v28.html#status-429) · [page](../../current/states/status-429.html) | [1580](../../current/v28-shots/s73-status-429-1580.png) · [1440](../../current/v28-shots/s73-status-429-1440.png) · [760](../../current/v28-shots/s73-status-429-760.png) |
