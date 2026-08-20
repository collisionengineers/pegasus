# Files — PR-027

| File | Change/risk |
|---|---|
| `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs` | Add list/update validation and resolver authorization/fail-closed cases. |
| `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryPersistenceTests.cs` | Add version/operation conflicts, competing update, exact history snapshots and retained-row assertions. |
| `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs` | Add authenticated disable, validation, stale conflict, replay/recovery and denied POST. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Prove exact Web grants, Web DELETE denial and no Worker grant. |
| Production catalogue files | Change only if a focused test reveals a correctness defect. |
| MAIL-004 PIR | Replace broad claims with named, exact commands/results. |

## Context

Reuse `AdministrationPolicyPersistenceTests.cs`, `IntakeWebTestSupport.cs`, `AdministrationPageModel.cs`, and the existing LocalDB/runtime-role query helpers.

Out of scope: a new test framework, Graph calls, external writes, search/linking, and message mutation.
