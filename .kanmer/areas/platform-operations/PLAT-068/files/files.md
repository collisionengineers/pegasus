# Files — PLAT-068 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Identity/StaffAccountAdministration.cs` | change | Add account sign-off value, mutation contract, validation, and query exposure. | `StaffAccountAdministrationPolicy`, `IStaffAccountQueries` |
| `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs` | change | Persist the reasoned, idempotent sign-off mutation and action history. | Existing serializable transaction and `AddHistory` pattern |
| `src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs` | change | Project sign-off data into the Core account query result. | `Summary`, `ParseRole` |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | change | Add identity-user properties and EF limits/mapping. | `PegasusIdentityUser` mapping |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | change | Register any new sign-off store/use case introduced by the Core contract. | Existing staff-account registrations |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_StaffAccountSignOff.cs` | create | Add sign-off columns to `AspNetUsers`, serialized after `20260829212237_GrantProviderSubmissionAcceptRecovery`. | Existing SQL Server/provider and grant convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_StaffAccountSignOff.Designer.cs` | create | EF migration metadata. | EF migration generation |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | EF model snapshot for the new identity fields. | EF migration generation |
| `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml.cs` | change | Add Administrator-only load/post handling for sign-off settings. | `RunAsync`, `Validate`, `NewOperationKey` |
| `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml` | change | Add the sign-off table column and settings UI, with no explanatory copy. | Existing account row/forms and `_ReasonDialog` pattern |
| `src/Pegasus.Web/Pages/Administration/Accounts/Edit.cshtml`, `Edit.cshtml.cs` | change (conditional) | Only if the plan hosts the per-account settings surface on the existing Edit route instead of the Index. | Existing `OnPostDisableAsync` reason/operation-key shape |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change (shared lock, capacity one) | Centralize new operator words. Add `StaffAccounts.SignOffEngineer`, `SignOffEngineerYes`, `SignOffEngineerNo`, `Qualifications`, `SignatureImage`, `SignatureOnFile`, `SignatureNotOnFile`, `UploadSignature`, `ReplaceSignature`, and `Save`. | `OperatorLabels.StaffAccounts` |
| `tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs` | change | Prove authorization, normalization, Engineer-only eligibility, and reason/operation-key forwarding. | Existing recording store |
| `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs` | change | Prove Administrator-only, reasoned account update, persisted sign-off state, and action history. | Existing Accounts web harness |
| `tests/Pegasus.Core.Tests/Identity/ActorDisplayNamesTests.cs`, `Intake/RetainedMailTests.cs`, `Reports/EngineerActivityReportTests.cs`, `Triage/GetTriageDisplayNameTests.cs` | change (conditional) | Each constructs `new StaffAccountSummary(...)`; touched only if the record gains positional fields. | Existing fixtures |
| `docs/design/test-ui/pages/administration-accounts--default.html` | change (shared lock, capacity one) | Capture the changed populated Accounts page. | `Update-TestUiSnapshots.ps1` |
| `docs/design/test-ui/catalogue.json` | change (conditional) | Only if a new snapshot scenario (e.g. settings dialog open) is added. | `Test-UiCatalogue.ps1` |

The existing table-level `AspNetUsers` grants already cover new columns, so no
separate grant-only migration is warranted unless the chosen schema creates a
new table.

## Must not touch (owned by another EPIC-012 lane)

DOCS-017 owns renderer tuple acceptance and embedded signature selection:

- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`
- `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`
- `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
- `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`

CASE-040 owns the case field, default rule, ribbon, and EVA dialog:

- `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`
- `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/*`
- `docs/design/test-ui/pages/case-details--*.html`
- `docs/design/test-ui/pages/case-eva-send--default.html`

PLAT-064 owns the Administrator password reset (D28); this ticket adds no
reset handler.
