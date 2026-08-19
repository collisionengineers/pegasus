# Post-implementation report — INTK-005

## Summary

Authenticated Upload now accepts multiple files in one submission, creates one durable submission group, preserves an independent staged receipt/work item and original filename for every member, and presents the whole group with links to each receipt status. Replay uses a stable form token with deterministic ordinal child identities, so retries do not duplicate accepted files.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/GroupedIntake.cs` | Added group/member contracts, group-store port, and sequential grouped submission use case. | Supplies one durable producer boundary while reusing existing per-file intake. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs` | Added EF group/member persistence, replay lookup, ordered member query, and constraints. | Preserves group identity and per-file receipt identity durably. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Added group/member entities and mappings. | Enforces unique submission token, ordinal, and receipt membership. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819101344_GroupedIntakeSubmission.cs` and designer/snapshot | Added schema migration. | Makes group identity deployable and reviewable. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` and `src/Pegasus.Web/Program.cs` | Registered the group store and use case. | Makes the Core port available to Web and future Worker consumers. |
| `src/Pegasus.Web/Pages/Upload.cshtml(.cs)` | Changed binding/validation to a file collection and redirects to group status. | Removes the one-file interaction limit while retaining per-file safety limits. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Extended the existing PLAT-006 dropzone readout/drop handling to multiple files. | Keeps the merged visual/accessibility convention and no-JavaScript fallback. |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml(.cs)` | Added group result page listing every member and current receipt status link. | Prevents partial success from being hidden behind the last receipt. |
| `tests/Pegasus.Core.Tests/Intake/GroupedIntakeTests.cs` | Added ordering, replay, and conflict unit coverage. | Protects the group identity contract. |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | Added group-aware queue draining and multi-file multipart helper. | Keeps integration tests aligned with grouped redirects. |
| `tests/Pegasus.IntegrationTests/GroupedIntakeWebTests.cs` | Added SQL-backed multi-file group test. | Proves two filenames become one group with independent receipts. |

## Governing docs

- FRD-02: per-file source identity, durable custody, idempotent replay, and existing intake limits remain owned by `ReceiveIntake`; the new group relation only correlates members.
- FRD-12: the Upload surface visibly lists all selected files and exposes the next receipt/status action.
- No governing document was modified and no ADR was required. The group relation stays inside the existing Core, Infrastructure, and Web boundaries.

## Risks / follow-ups

- INTK-006 must consume the new group query before vehicle-image routing can be complete; the ticket remains the next dependency.
- The full IntegrationTests run was attempted but its test host crashed after 61 passed tests; focused grouped web, focused Core, architecture, and Release build checks passed. Verify the complete suite on merged main.
- Runtime-role grants for the new tables should be confirmed against the deployment migration conventions before production promotion.

## Verification hand-off

On merged `main`, run:

- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test --configuration Release`
- Focus `GroupedIntakeTests` and `GroupedIntakeWebTests`
- Exercise Upload with two files, duplicate filenames, exact replay, and a partial worker failure.
- Capture a browser screenshot showing the selected multi-file list and group status page with every member linked.
