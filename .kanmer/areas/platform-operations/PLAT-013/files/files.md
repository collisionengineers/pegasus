# Files — PLAT-013 worker SIGABRT crash loop

## Production evidence (read-only, App Insights app b2c7c738-3b1d-4018-8dc1-99e704f19e72)

- All exit-134 aborts (7,582 exception rows, 2026-08-10 → 2026-08-18) carry ONE innermost message: `Unhandled exception. System.InvalidOperationException: Box:ConfigJson is not a valid Box JWT configuration.`
- Aborts are two bursts, not a continuous loop: 2026-08-10 18:41–19:5x (2,544; initial prod provision) and 2026-08-18 11:51:00–11:54:51 (344 across 25 role instances; `azd provision` was rewriting role assignments/sites 11:49–11:54 in the activity log).
- Worker dies ~250 ms after the host launches it, before any function executes ("Failed to start language worker process for runtime: dotnet-isolated" … "Language Worker Process exited"), then the host retries into the same environment — the loop.
- No image/ONNX/timer correlation: during the 08-18 burst only 25 timer executions ran, zero IntakeWorkFunction. Plan is Flex Consumption, 2048 MB, max 20 instances, siteUpdateStrategy=Recreate — memory is not the cause.
- `Box__ConfigJson` / `Box__ClientSecret` are `@Microsoft.KeyVault(...)` references. During provisioning the platform hands the worker the unresolved literal (or an empty value); the eager JSON parse throws; .NET aborts the process (exit 134).

## Files to change

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | `BoxCustodyOptions.Create`: name an unresolved `@Microsoft.KeyVault(` placeholder explicitly instead of "not a valid Box JWT configuration". |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | `AddProductionDocumentStorage` / `AddProductionBoxCustody` take `Func<IServiceProvider, BoxCustodyOptions>`; options parse becomes lazy (first Box resolution), not host-build. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Drop eager `BoxCustodyOptions.Create` from `GetProductionExternalOptions`; pass a deferred factory to `AddProductionDocumentStorage`. |
| `src/Pegasus.Web/Program.cs` | Same deferral for the Web composition root (same latent bug: unresolved reference kills site start). |
| `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` | Adapt to factory signature; new test: invalid/unresolved Box config no longer fails host build, fails closed at first Box resolution. |
| `tests/Pegasus.IntegrationTests/ProductionBoxCustodyTests.cs` | New cases: unresolved Key Vault placeholder is named in the error for ConfigJson and ClientSecret. |
