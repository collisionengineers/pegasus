# Files — INTK-039

## Change locations

| Area | Purpose and risk |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/Migrations/` | Add the Worker lifecycle-event grant. Migration order must follow PR #544 and Down must revoke only this grant. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Keep bootstrap validation equal to the migration’s effective runtime-role contract. |
| `src/Pegasus.Web/Presentation/UploadOutcome.cs` and `Pages/UploadGroupStatus.cshtml.cs` | Render unresolved grouped image material as Working and keep polling without changing terminal group outcomes. |
| Existing integration tests | Prove role grants, group polling/actions, lifecycle/custody completion, and queue count/row agreement. |

## Context files

| File | Authority |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Existing atomic lifecycle/history/custody-work transaction to reuse unchanged. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Existing group routing and association owner; no second policy implementation. |
| `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` | Existing bounded group-pending recovery mechanism. |
| `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` and `EfImageIntakeStore.cs` | Explain the count/list symptom; neither should receive a masking exception. |
| `tests/Pegasus-Test-Logs/basic-intake-match-testing/` | Operator evidence and live acceptance journeys. |

## Ripple effects

- Update `AzureSqlRuntimeRoleMigrationTests`, the migration census in `IntakePersistenceIntegrationTests`, upload confirmation tests, and the existing image lifecycle/custody/queue integration coverage.
- A grant-carrying migration must be mirrored by the deployment-plan/bootstrap guard.
- Production deployment must apply the bundle and then read back effective Worker permissions.

## Out of scope

- Repairing linked production records, changing Case/Image Intake schemas, redesigning queue counts, adding a Box fallback, changing Outlook, deleting Box folders, or adding compatibility paths.
