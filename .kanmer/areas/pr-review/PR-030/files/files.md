# Files

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Add exact folder identity to the existing MIME GET and Deleted caller. Inbox callers continue using mailbox-global immutable reads. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Assert folder-scoped MIME path and simulate a post-enumeration move returning 404/unavailable. |

Out of scope: retries, mutation, new Graph permissions, or another client.
