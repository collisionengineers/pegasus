# Files

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Restrict receipt projection admission to attachment content so every admitted retained row has a visible label. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Prove root-only projection text cannot admit an unlabeled row while retained body and attachment matches remain. |

Context: `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` still supplies Deleted canonical body and retained attachment content. Out of scope: deleting root documents, a second projection, or backfill.
