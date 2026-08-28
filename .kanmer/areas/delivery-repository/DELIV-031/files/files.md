# Files

## Changed

- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` — the
  only file in scope. `LocalDbTestDatabase.BuildConnectionString` (the single
  `SqlConnectionStringBuilder` shared by `CreateConnection()`,
  `MasterConnectionString()`, and every `UseSqlServer` composition) and
  `LocalDbTestDatabase.DisposeAsync()` (the `DROP DATABASE` path).

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
  `CustodyOutboxIntegrationTests.cs`) — none of these set an explicit
  `ConnectTimeout`/`Connect Timeout`, so none compete with or defeat this
  fix. They are not the flaking harness named in the ticket's evidence and
  are out of scope for DELIV-031.

## Out of scope (per ticket's Do NOT)

- `.github/workflows/repository-check.yml`
- `scripts/Invoke-TestShard.ps1`
- Any `src/` file
