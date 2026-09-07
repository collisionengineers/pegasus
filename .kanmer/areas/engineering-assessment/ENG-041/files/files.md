# ENG-041 affected files

- `src/Pegasus.Core/Assessment/GlassRepairEstimates.cs`
- `src/Pegasus.Core/Assessment/Estimates.cs`
- `src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs`
- `src/Pegasus.Infrastructure/Glass/GlassMvaClient.cs`
- `src/Pegasus.Infrastructure/Persistence/EfGlassRepairEstimateSessionStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml`
- `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs`
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md`
- `tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimateGatewayTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimatePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`

Gateway production caller: Cases/Details LaunchGlass, ResumeGlass and new explicit Close session POST; anonymous callback unchanged. Estimate production caller: Cases/Details SaveEstimate to SaveEstimate to EfRepairSpecificationStore. Existing registrations remain. Generated case-details snapshots are root-verifier-owned and will be added to scope before capture if needed. No package or schema changes.

Root-verifier generated artifacts: `docs/design/test-ui/pages/case-details--*.html`
and `docs/design/test-ui/index.html` for the changed routed Case estimate UI.
