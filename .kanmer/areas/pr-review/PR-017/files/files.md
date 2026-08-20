# Files — PR-017

| Path | Change / risk |
|---|---|
| `src/Pegasus.Core/Intake/DeletedMailSearch.cs` | Reuse the Deleted source port for approved mailbox refinements. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Return the existing approved-estate records, no duplicate owner. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Select Deleted mailbox tabs from the Deleted boundary only. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`, `ProductionGraphSourceTests.cs` | Empty approved mailbox scope evidence. |

Context: retained mailbox listing remains unchanged for Inbox/Sent. Out of scope: invented retained rows.

## Re-review file delta

- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — add the missing authenticated zero-retained-row mailbox caller proof. No production file changes.
