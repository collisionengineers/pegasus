# Files — PR-038

| Path | Change |
|---|---|
| `MailboxModelConfiguration.cs`, MAIL-07 migration/designer/snapshot | Add the filtered per-message active-operation unique index. |
| `EfRetainedMailFolderMoveStore.cs` | Treat a database claim loss as refusal before provider invocation. |
| `RetainedMailPersistenceTests.cs` | Prove concurrent different keys produce one provider call; preserve replay and retry. |

Out of scope: distributed command framework, background worker, live Graph.
