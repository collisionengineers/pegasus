# Files — PR-043

## Changed files

| File | Change | Risk |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` | Separate Pending replay refusal from Uncertain recovery; persist Uncertain before post-provider probe. | Concurrency/state transition; exact test required. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Add overlapping same-key/new-key proof using the existing blocking mover. | LocalDB timing must be deterministic. |

## Context files

| File | Why read |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Confirms Pending and Uncertain retain the filtered active slot. |
| `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs` | Existing request validation and exception surface; no contract expansion needed. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Genuine Uncertain same-key destination/source/unresolved recovery must remain unchanged. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Governs explicit, duplicate-safe and recoverable move behavior. |

## Out of scope

No new state vocabulary, lease, worker, framework, endpoint, provider call, live mailbox action, permission or deployment change.
