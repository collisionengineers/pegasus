# Files

## Modify

- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — validate Deleted folder root object, folder page root/value array, and present next-link absolute URI at the existing client boundary.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` — prove folder-root, missing/non-array value, and malformed/relative next-link cases map to unavailable.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — prove representative malformed envelope and next-link failures render the authenticated unavailable state.

## Overlap and dependencies

All three files are already in [[TICK-053]] / PR #469 and are intentionally shared. This completes [[PR-033]]; it adds no file to the 31-file inventory and depends on no unlanded code.
