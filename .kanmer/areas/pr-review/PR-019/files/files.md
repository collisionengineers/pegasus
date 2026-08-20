# Files — PR-019

| Path | Change / risk |
|---|---|
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Normalize/validate GET input into a supported page state. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Render explicit no-match/invalid-query status. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Populated no-match and overlong direct-query coverage. |

Context: Core retains authoritative 1–200 validation. Out of scope: changing search semantics.
