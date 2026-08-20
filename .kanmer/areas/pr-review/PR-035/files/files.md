# Files

## Modify

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — reuse the GET handler's invalid-search response in `ReloadAsync`.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — submit authenticated invalid-correction POSTs with whitespace and overlong search context.

## Overlap and dependencies

- Both files are already owned by [[TICK-053]] / PR #469.
- Depends only on the landed Core retained-search validator. No new validator or UI state.
