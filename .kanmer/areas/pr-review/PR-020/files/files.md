# Files — PR-020

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Map provider timeout only when caller cancellation was not requested. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Timeout unavailable and caller cancellation propagation. |

Context: existing unavailable state is Core-owned. Out of scope: retry framework.
