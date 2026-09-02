# Files — TICK-060

## Change map

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs` | Reuse the existing result query and collapse its public result to unfinished, Case/PO success, or terminal failure while retaining Principal ownership policy. |
| `src/Pegasus.Infrastructure/Persistence/EfProviderSubmissionStore.cs` | If needed, scope the existing submission lookup by Principal; add no second projection or store. |
| `src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs` | Keep the existing GET route and map empty 202, identifier-only 200, generic 422, and indistinguishable 404. |
| `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs` | Pin Core ownership and the three result outcomes. |
| `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | Pin the public status/body contract, paused reads, revoked authentication, and cross-Principal nondisclosure. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Make API-03 the identifier-only result contract and remove API-02-style processing detail. |
| `docs/capabilities.md` | Record API-03 as the result owner and API-02 detailed status as retired. |

## Existing seams reused

- `IGetProviderSubmissionResult` and `GetProviderSubmissionResult`
- `IProviderSubmissionStore`
- `IQueuedIntakeStatusQueries`
- `IIntakeReceiptQueries`
- `ProviderApiEndpoints.MapPegasusProviderApi`
- API-01 authentication, rate limiting, feature composition, and test helpers

## Explicitly unchanged

No new route, result store, SQL projection, table, migration, queue, resource,
dependency, webhook, general Case lookup, report/file surface, or deployment.
