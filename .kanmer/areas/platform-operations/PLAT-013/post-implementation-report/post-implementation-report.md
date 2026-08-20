# Post-implementation report — PLAT-013

## Root cause, with the correlated production evidence

**The worker's composition root parsed the Box Key Vault secret eagerly at host build; whenever App Service handed the process the unresolved `@Microsoft.KeyVault(...)` placeholder, the parse threw an unhandled `InvalidOperationException` out of `Program.Main`, .NET aborted the process (SIGABRT → "dotnet exited with code 134"), and the Functions host restarted it into the same environment — the crash loop.**

Evidence chain (all read-only App Insights `b2c7c738-3b1d-4018-8dc1-99e704f19e72` + activity log):

1. Every exit-134 abort exception over 10 days — 7,582 rows — carries one innermost message: `Unhandled exception. System.InvalidOperationException: Box:ConfigJson is not a valid Box JWT configuration.` Monocausal; no other message appears.
2. The aborts are two bursts, not a continuous loop: 2026-08-10 18:41–19:5x (2,544) and 2026-08-18 11:51:00–11:54:51 (344 across 25 role instances — the ticket's 48 h count). Zero aborts outside those windows.
3. Both bursts align exactly with `azd provision` runs: the activity log shows role assignments, `sites/write` and `serverFarms/write` executing 2026-08-18 11:49–11:54 and 2026-08-10 18:44–18:45 — the exact burst minutes.
4. Instance traces show the worker dying ~250 ms after launch, before any function executes ("Failed to start language worker process… Language Worker Process exited"), with `ConsecutiveErrors` climbing then "proactively recycling the Functions Host". The 393 `JobHost.StopAsync` "host has not yet started" failures are this same loop's teardown noise, not a separate fault.
5. Candidates eliminated: no IntakeWorkFunction/ALPR ran during the 08-18 burst (only 25 timer executions); plan is Flex Consumption `instanceMemoryMB` 2048, max 20 instances — not memory; no ONNX/SkiaSharp frames anywhere in the abort records.
6. The other eager option parsers (Graph, Dvla/Dvsa) only check non-emptiness, so an unresolved placeholder passes startup there and fails per call — consistent with Box being the only startup abort.

## What changed

- `src/Pegasus.Infrastructure/DependencyInjection.cs` — `AddProductionDocumentStorage` / `AddProductionBoxCustody` now take `Func<IServiceProvider, BoxCustodyOptions>`; the parse runs at first Box resolution (MEDI lazy singleton factory — a failed factory is retried on the next resolve). A bad/unresolved secret fails the Box work item closed through the existing queue retry/poison path; mail polling, intake and ALPR keep running.
- `src/Pegasus.Worker/WorkerDependencyInjection.cs` — eager `BoxCustodyOptions.Create` removed from `GetProductionExternalOptions`; deferred factory passed instead; `Box` dropped from `ProductionExternalOptions`.
- `src/Pegasus.Web/Program.cs` — same deferral (same latent bug killed Web site start); `documentStorage:` gated on `productionProfile`.
- `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` — `Create` names an unresolved `@Microsoft.KeyVault(` placeholder explicitly for ConfigJson and ClientSecret, so the next occurrence is searchable instead of masquerading as malformed JWT material.
- Tests: `ProductionBoxCustodyTests.ConfigurationNamesAnUnresolvedKeyVaultReferenceDirectly`; `ProductionCompositionTests.AnUnresolvedBoxSecretFailsTheFirstBoxUseNotHostBuild`; existing call sites adapted to the factory signature.

## Verification

- `dotnet build ./Pegasus.slnx -c Release` — 0 warnings, 0 errors.
- Focused `dotnet test`: Pegasus.IntegrationTests filter `ProductionBoxCustody|ProductionComposition` → 19/19 passed; Pegasus.ArchitectureTests (full, includes WorkerCompositionTests over `AddPegasusWorker`) → 97/97 passed.
- PR #438 → dev.

## Deploy-time note

**No Azure write is required.** The provisioning-window secret turbulence is a platform behaviour; the fix converts it from a process-wide abort loop into per-work-item Box failures that retry. The ticket's remaining verification boxes (exit-134 silence over a multi-hour window; grouped uploads completing on attempt 1) are provable only after the next release deploys — note that App Insights ingestion for this resource stopped at 2026-08-19 11:49 UTC (daily-cap signature), so that check needs the cap window to roll over or the cap raised (a read-only query confirms either way).

## Ticket-body correction

The ticket said "crash-looping continuously": production shows the loop only runs during provisioning windows and the estate is quiet otherwise. The 2026-08-20 02:55 grouped-upload attempt-1 failures happened OUTSIDE any abort burst, so their no-FailureCode signature was not caused by these SIGABRTs; that defect remains with the intake-retry ticket ([[INTK-015]] area).
