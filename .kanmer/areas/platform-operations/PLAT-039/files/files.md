# Files

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | `BoxJwtAuthorizationHeaderProvider` caches the header with the expiry Box states and renews before it lapses. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Registration passes `TimeProvider` (already a singleton from `AddInfrastructure`, resolved this way at `:594`). |
| `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml.cs` | Add `HttpRequestException` to the catch list so a Box transport failure lands on the case page, not the error page. |
| `tests/Pegasus.IntegrationTests/BoxAuthorizationHeaderTests.cs` | New. The provider has never had a test. |

## Not changed, and why

- **`BoxContentClient.SendAsync`** — the single point every Box call passes
  through, so a 401-and-retry would also work there. It is not the chosen
  shape: an upload's `HttpContent` wraps a forward-only stream, so replaying
  the request is not free, and the failure is a *predictable* lifetime, not an
  unpredictable rejection. Renewing on the clock removes the failure instead of
  recovering from it.
- **`Box.Sdk.Gen`'s `TokenStorage`** — left alone. The provider stops asking it
  for a cached token and calls `RefreshTokenAsync` on its own schedule, so the
  SDK's no-expiry cache is simply no longer in the path.
- **The Worker** — carries the same provider and gets the same fix; no
  Worker-side change exists to make.
