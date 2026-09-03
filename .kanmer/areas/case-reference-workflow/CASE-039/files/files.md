# Files — CASE-039 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Files

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Cases/EngineerNotes.cs` | create | Add/list contracts, records, validation, staff-only authorization, and append-only policy. | `CaseNotes.cs` |
| `src/Pegasus.Infrastructure/Persistence/EngineerNoteEntities.cs` | create | Define the separate persisted Engineer-note row. | `AssessmentEntities.cs` |
| `src/Pegasus.Infrastructure/Persistence/EngineerNotesModelConfiguration.cs` | create | Map `EngineerNotes`, constraints, relationship, and newest-first index. | `AssessmentModelConfiguration.cs` |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | change | Register the entity set and configuration. | Existing DB-set/configuration pattern |
| `src/Pegasus.Infrastructure/Persistence/EfEngineerNoteStore.cs` | create | Implement add/list ports, attribution, replay protection, and separate projection. | `EfCaseNoteStore.cs` |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | change | Register the Engineer-note port implementations and use cases. | Existing Case-note registrations |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_EngineerNotes.cs` | create | Create the table and grant the web runtime role. | `CaseValuations` migration |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_EngineerNotes.Designer.cs` | create | EF-generated migration model. | Existing migration designers |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Record the current EF model. | Existing snapshot |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change | Load notes and add the POST handler. | `TasksModel.OnPostAddNoteAsync` / `ExecuteCaseCommandAsync` |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | change | Route the Engineer-notes section to its partial. | Existing section dispatch |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseEngineerNotes.cshtml` | create | Render separate rows and add form without empty-state prose. | `_CaseHistory.cshtml` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` | change | Add the Engineer-notes section link. | Existing sections tuple |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Add all operator-facing Engineer-note labels in one place. | `CaseWorkspace` |
| `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs` | create | Prove validation, staff-only authorization, ordering request, and append-only contract. | `AddCaseNoteTests.cs` |
| `tests/Pegasus.IntegrationTests/EngineerNotePersistenceTests.cs` | create | Prove separate-table persistence, attribution, replay protection, ordering, and absence from `CaseWorkflowEvents`. | `CaseNotePersistenceTests.cs` |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change | Prove section rendering, add handler, terminal-state rule, and no Notes-history leakage. | Existing Case Details tests |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | change | Exercise the Case-page Engineer-notes journey if the section is included in the browser fixture route. | Existing Case workspace journey |
| `docs/design/test-ui/pages/case-details--default.html` | change | Refresh the Case Details snapshot after adding the navigation entry. | `Update-TestUiSnapshots.ps1` |

**Files this ticket must not touch because another verified EPIC-012 lane owns
them (Codex remote-branch view, superseded by the board table below):** the
remote-branch comparison found no listed CASE-012, CASE-027, ENG-027, or
PLAT-029 branch ahead of `origin/dev`. Before taking the ticket, the
implementer must still obtain the shared-lock slots for
`_CaseWorkspaceNav.cshtml`, `OperatorLabels.cs`, migrations, and Test UI
snapshots.

### Wrapper corrections (Claude, 2026-09-02)

- `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` and
  `docs/design/test-ui/pages/case-details--default.html`: change only if the
  wave's shared-lock holder (UIIMP-014 owns `docs/design/test-ui/**`) agrees;
  the default snapshot regenerates with `./scripts/Update-TestUiSnapshots.ps1`
  whenever the routed page changes and is committed with the page change.
- `Pages/Cases/Details.cshtml`, `Details.cshtml.cs` and
  `_CaseWorkspaceNav.cshtml`: extended after CASE-038 merges, never in
  parallel with it.
- If the design README's "Add note (editing only)" is followed, the Details
  handler uses `ExecuteCaseCommandAsync` with the lease token rather than the
  lease-free `TasksModel.OnPostAddNoteAsync` shape named above.

### Files this ticket must NOT touch (board lane owners, EPIC-012)

| Path | Owner | Why |
| --- | --- | --- |
| `src/Pegasus.Web/wwwroot/css/site.css`, `wwwroot/js/site.js` | CASE-038 | Frame vocabulary (section-nav, case-sticky); notes list reuses existing classes. |
| `src/Pegasus.Web/Pages/Cases/Details.*`, `Cases/Shared/_CaseWorkspaceNav.cshtml` before CASE-038 merges | CASE-038 | Blocks CASE-039; section slot and lazy fetch handler land there first. |
| `docs/design/test-ui/**` new states and `catalogue.json` entries | UIIMP-014 | Snapshot states for every section belong to that chore. |
| `src/Pegasus.Web/Pages/Cases/Assessment/**`, extracted Engineer partials | ENG-034 | Workbench move. |
| `src/Pegasus.Core/Assessment/**`, `Core/Reports/AssessmentReportProjection.cs` | ENG-035 | Assessment vocabulary and report projection. |
| `Cases/Shared/_CaseInspectionAddress.cshtml`, storage-location column | CASE-041 | Inspect-at fast update. |
| `Pages/Cases/Eva/Send.*`, sign-off Engineer field | CASE-040 | Sign-off tuple. |
| `src/Pegasus.Core/Cases/CaseNotes.cs`, `Persistence/EfCaseNoteStore.cs`, `Cases/Shared/_CaseHistory.cshtml` | existing Notes history | Reused as a pattern, not modified: Engineer notes must stay out of the Notes history. |
| `docs/frd/**`, `docs/design/README.md`, `docs/capabilities.md` | DELIV-041 (PR #647) | Governing text for D32 is already written there. |
| `docs/operator-notes.md`, `corpus/` | protected | Never. |
