# Files

Revised 2026-08-29 after adversarial verification.

## Changed

- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` — the
  only file in scope. `LocalDbTestDatabase.BuildConnectionString` (the single
  `SqlConnectionStringBuilder` shared by `CreateConnection()`,
  `MasterConnectionString()`, and every `UseSqlServer` composition) and
  `LocalDbTestDatabase.DisposeAsync()` (the `DROP DATABASE` path).

Net: +27/-2, one file.

## Does the patched builder actually reach the failing tests?

Added this round. The first version of this document argued only that no
*other* connection string competes with the fix; it never asked the coverage
question, which is the one that matters. Checked by grep, per class named in
the ticket's evidence and in the 5061 log:

| Failing class | How it reaches `BuildConnectionString` |
| --- | --- |
| `OrganizationAdministrationWebTests` | `new IntakeWebApplicationFactory()` (`:17`, `:143`) |
| `AutomationConnectorAuthorizationTests` | `new IntakeWebApplicationFactory(TimeProvider.System)` (`:31`, `:125`, `:149`) |
| `CaseDetailsWebTests` | `IntakeWebApplicationFactory` via `CaseCapabilityPagesTestSupport.cs:22` |
| `CaseTaskArchivePersistenceTests` | `LocalDbTestDatabase.CreateAsync(...)` directly (`:801`) |
| `IntakePersistenceIntegrationTests` | `LocalDbTestDatabase` directly |

`IntakeWebApplicationFactory` holds
`private readonly LocalDbTestDatabase database` (`IntakeWebTestSupport.cs:41`),
constructed at `:92`. So every class in the evidence routes through the one
patched builder — the fix is not confined to the file it edits.

## Confirmed clean (searched, not touched)

Searched `tests/` for `ConnectTimeout`, `Connect Timeout`, `Data Source=`, and
`Server=` to rule out a second, competing connection-string builder:

- `tests/Pegasus.IntegrationTests/ReadinessEndpointTests.cs:82,141` — hardcode
  `Connect Timeout=1` against `Server=127.0.0.1,1`, an intentionally
  unreachable loopback port used to exercise readiness-probe failure paths.
  Not the same concept as the LocalDB harness; left alone.
- Various `Server=(localdb)\MSSQLLocalDB;...` literals in
  `Pegasus.ArchitectureTests` and other `Pegasus.IntegrationTests` files
  (`CaseWorkflowMigrationTests.cs`, `LocalIntakeAccessTests.cs`,
  `ProductionCompositionTests.cs`, `Reports/AssessmentReportRendererTests.cs`,
  `WorkerCompositionTests.cs`, `WorkerAzureClientCompositionTests.cs`,
  `CustodyOutboxIntegrationTests.cs`) — none sets an explicit
  `ConnectTimeout`/`Connect Timeout`, so none competes with or defeats this
  fix.

## Read for evidence, not modified

- `.github/workflows/ci.yml` — `sql-integration` job (`:149-183`). Establishes
  that each of the three shards gets its own `windows-latest` runner, that the
  job timeout is 20 minutes, and that no step starts or waits on LocalDB.
- `scripts/Invoke-TestShard.ps1` — no LocalDB startup or readiness handling.
- `tests/Pegasus.IntegrationTests/xunit.runner.json` —
  `maxParallelThreads: 4`, the actual source of the contention.
- `tests/Pegasus.IntegrationTests/LocalDbTemplateDatabase.cs:223-241` —
  `DropQuietlyAsync` runs the same `ALTER`+`DROP` SQL. Considered as a shared
  retry site and rejected: it is a best-effort sweep of a previous run's
  leftovers that deliberately swallows every exception.

## Out of scope

- `.github/workflows/ci.yml`, `scripts/Invoke-TestShard.ps1`, and any `src/`
  file.

Correction: the first version of this document attributed that exclusion to
"the ticket's Do NOT list". The DELIV-031 record has no such list — it has
What / Why / Approach / Verification. The constraint came from the
orchestrator's lane brief.
