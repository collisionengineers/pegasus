# Files

| Path | Change / risk |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Carry a validated optional search term through existing detail use case/query contract. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Reuse existing single-row match mapping for detail. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Include active-search membership in the existing outside-scope state. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Authenticated matching-to-nonmatching thread navigation proof. |

Context: `Message.cshtml` already renders the required outside-view status and preserves Back/search. Out of scope: a second query service or changing thread membership.
