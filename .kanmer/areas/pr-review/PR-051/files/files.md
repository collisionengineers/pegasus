# Files

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — extend and validate the existing protected association payload; keep exact replay authority after success.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — require the matching action discriminator before rendering a final dialog.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — authenticated cross-message and both cross-action refusal tests plus unchanged exact replay.

Unchanged: Core commands/fingerprint, EF/schema/store, shared search/reason UI, external adapters.
