# Files

| Path | Change / risk |
|---|---|
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Add authenticated route proof and one narrow source fake using existing test-host override conventions. |

Context: `src/Pegasus.Web/Pages/Mail/Index.cshtml(.cs)` and `src/Pegasus.Core/Intake/DeletedMailSearch.cs` define the caller/states being proved. Out of scope: Graph calls, persistence, a new use case, or production fake.
