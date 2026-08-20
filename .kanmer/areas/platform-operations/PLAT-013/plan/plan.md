# Plan — PLAT-013 stop the worker SIGABRT crash loop

## Root cause (proved, not speculated)

`WorkerDependencyInjection.AddPegasusWorker` → `GetProductionExternalOptions` calls `BoxCustodyOptions.Create` **eagerly during host build**. `Box:ConfigJson` is a Key Vault reference; while an `azd provision` is rewriting role assignments/recreating the site, App Service hands the worker the unresolved `@Microsoft.KeyVault(...)` literal. `JsonDocument.Parse` fails → `InvalidOperationException` escapes `Program.Main` → the runtime aborts the process → `dotnet exited with code 134 (0x86)`. The Functions host restarts the worker into the same environment → crash loop until resolution stabilises. Every one of the 7,582 abort exceptions carries this innermost message; both bursts align with provisioning windows; the worker dies ~250 ms after launch, before any function runs. Not ONNX/SkiaSharp, not memory (Flex 2048 MB), not timers.

## Fix: defer Box configuration to first Box use; fail closed per work item, not per process

A bad Box secret must not kill mail polling, intake and ALPR. Box work that hits the bad config fails its own invocation (queue retry/poison — the existing fail-closed path); everything else keeps running.

1. **`BoxCustodyOptions.Create`** (`src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs`): before JSON parse, reject values starting `@Microsoft.KeyVault(` for ConfigJson/ClientSecret with "unresolved Key Vault reference" messages. Reuses the method's existing guard style. This makes the true cause searchable in App Insights instead of "not a valid Box JWT configuration".
2. **`AddProductionDocumentStorage` / `AddProductionBoxCustody`** (`src/Pegasus.Infrastructure/DependencyInjection.cs`): parameter becomes `Func<IServiceProvider, BoxCustodyOptions>`; registered via `services.AddSingleton(boxOptions)` so MEDI's lazy singleton factory (the host's own mechanism — no new wrapper type) runs Create at first Box resolution. A failed factory is retried on the next resolve.
3. **Worker** (`src/Pegasus.Worker/WorkerDependencyInjection.cs`): remove `Box` from `ProductionExternalOptions`; pass `_ => BoxCustodyOptions.Create(configuration["Box:BaseUri"], …)` to `AddProductionDocumentStorage`.
4. **Web** (`src/Pegasus.Web/Program.cs`): replace the eager `productionBoxCustodyOptions = Create(...)` with the same deferred factory; gate `documentStorage:` on `productionProfile` instead of the options being non-null. The existing required-keys presence loop stays (fast failure for genuinely absent settings).
5. **Tests** (`tests/Pegasus.IntegrationTests/ProductionBoxCustodyTests.cs`, `ProductionCompositionTests.cs`): unresolved-placeholder messages; composition test proving a provider with a throwing Box factory still builds and resolves non-Box services, and throws only at `ICaseCustody` resolution. Reuses existing `BoxOptions()`/`BuildProduction()` helpers.

## Deploy-time note

No Azure change is required. The crash windows are inherent to provisioning (site Recreate + role-assignment rewrite); the code change turns them from a worker-wide abort loop into per-work-item Box failures that retry.

## Verification

- `dotnet build ./Pegasus.slnx -c Release` zero warnings.
- Focused: `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~ProductionBoxCustody|FullyQualifiedName~ProductionComposition"`.
- Post-deploy (out of this ticket's hands): exit-134 disappears from App Insights across the next release's provisioning window.
