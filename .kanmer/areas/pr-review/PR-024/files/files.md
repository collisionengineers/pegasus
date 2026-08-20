# Files

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Restrict receipt projection admission to attachment content so every admitted retained row has a visible label. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Prove root-only projection text cannot admit an unlabeled row while retained body and attachment matches remain. |

Context: `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` still supplies Deleted canonical body and retained attachment content. Out of scope: deleting root documents, a second projection, or backfill.

## Final re-review file delta

| Path | Change / risk |
|---|---|
| `src/Pegasus.Core/Intake/IntakeSearchProjection.cs`, `ProcessIntake.cs` | Normalize the single root projection using the existing route decision and select an attached-original body by its existing source label. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Use the same root projection for SQL body admission/match evidence and detail display, with historical fallback only. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs`, `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Prove wrapper/cid text is absent and displayed body equals the indexed root. |

No new column/table, parser, or backfill.
