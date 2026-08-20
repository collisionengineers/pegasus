# Files

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — return compensation outcome, preserve payload on release failure, and surface retry guidance.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — fail-once release decorator over the real port and exact retry/clear/immediate-editability proof.

Unchanged: Core lease policy/store, EF/schema, background processing and external systems.
