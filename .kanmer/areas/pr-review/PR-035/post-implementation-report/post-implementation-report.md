# Post-implementation report

Implemented in `fc6840361c1c19ece9a75d7ea68c713c75d01b75` on PR #469.

`MessageModel.ReloadAsync` now maps the existing Core overlong-search `ArgumentException` to the same 404 response as GET. Authenticated invalid-correction POST evidence proves whitespace-only search follows the existing normalized unfiltered page, overlong search returns 404, neither produces 500, and neither writes correction history.

Files: `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` and `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`.

Evidence: exact authenticated test passed; complete `MailWorkspaceWebTests + RetainedMailPersistenceTests` passed 39/39; Release solution build passed with 0 warnings/errors; `git diff --check` passed. No external write or new validator was added.
