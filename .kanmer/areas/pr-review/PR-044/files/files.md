# Files — PR-044

## Changed files

| File | Change | Risk |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` | Add bounded fresh-context Pending→Uncertain cancellation handoff before rethrow. | Must not downgrade committed Success or consume caller cancellation. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Add provider-cancel and success-save-cancel recovery tests using existing LocalDB/fake/interceptor conventions. | Cancellation timing must be exact and deterministic. |

## Context files

| File | Why read |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Uncertain retains the active filtered slot. |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | Existing SaveChangesInterceptor fault-injection convention. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Existing same-key destination/source/unresolved probe behavior remains authoritative. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Requires duplicate-safe recoverable external move behavior. |

## Out of scope

No worker, timer, lease, background task, retrying provider mutation, new state, migration, endpoint, permission, live mailbox or deployment change.
