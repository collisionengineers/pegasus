# Files — PR-015

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Use the existing DI fallback without overriding explicit production composition. |
| `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` (or existing composition test owner) | Prove default versus production resolution. |

Context: `src/Pegasus.Web/Program.cs` establishes actual registration order. Out of scope: Graph behavior, permissions or new composition framework.
