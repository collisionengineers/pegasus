# Files

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Map Azure Identity authentication failure at the existing Deleted boundary. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs`, `MailWorkspaceWebTests.cs` | Direct cancellation/failure proof plus authenticated rendered unavailable caller evidence using the real Graph source. |

Context: `Index.cshtml` already owns unavailable wording. Out of scope: retries, credential configuration, deployment, or permission changes.
