# Files — PR-016

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Separate bounded metadata listing from MIME reads and globally order candidates. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Two-mailbox fairness, global bound and truncation. |

Context: `DeletedMailSearch.cs` owns the fixed 100 limit. Out of scope: persistence, cursors or history reconstruction.
