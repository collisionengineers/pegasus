# Files — TICK-058

## Owns (whole files)

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs` | New: `SubmitProviderInstruction`, `GetProviderSubmissionResult`, records, `IProviderSubmissionStore`, `IProviderSubmissionBindings`, `ProviderSubmissionPolicy`. |
| `src/Pegasus.Infrastructure/Persistence/ProviderSubmissionEntities.cs`, `ProviderSubmissionModelConfiguration.cs`, `EfProviderSubmissionStore.cs` | New: the `ProviderSubmissions` table and its store/bindings adapter. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260828111707_ProviderSubmissions.*`, `20260828111732_GrantProviderSubmissions.*` | New: table + grants (Web SELECT/INSERT, Worker SELECT). |
| `src/Pegasus.Web/ProviderApi/ProviderApi.cs`, `ProviderApiAuthenticationHandler.cs`, `ProviderApiEndpoints.cs` | New: constants, the `PegasusProviderApi` bearer scheme, `/api/provider/v1/submissions` endpoints. |
| `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs`, `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | New tests. |

## Touched (shared files, narrow edits)

| Path | Edit |
| --- | --- |
| `src/Pegasus.Core/Identity/IdentityContracts.cs`, `StaffAuthorization.cs` | `ActorKind.Provider`, `ActionActor.Provider(principalId)`, `StaffAccessRight.SubmitProviderInstruction`. |
| `src/Pegasus.Core/Actors/ActorDisplayNames.cs`, `src/Pegasus.Core/Intake/RetainedMail.cs` | Provider label and `provider:` actor prefix (the one prefix map). |
| `src/Pegasus.Core/Intake/IntakeContracts.cs`, `DurableIntake.cs`, `GroupedIntake.cs`, `ProcessIntake.cs` | `IntakeSourceChannel.ProviderApi`; size bound; operation prefix; Principal binding from the credential, mail route skipped, no-policy → NeedsSorting. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`, `EfIntakeSubmissionGroupStore.cs`, `EfIntakeWorkStore.cs`, `EfTriageStore.cs`, `EfImageIntakeStore.cs` | `provider_api` channel code/parse (existing per-store maps). |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, `Migrations/PegasusDbContextModelSnapshot.cs`, `src/Pegasus.Infrastructure/DependencyInjection.cs` | DbSet, model configuration, registrations. |
| `src/Pegasus.Web/Program.cs`, `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `Features:ProviderApi` gate, per-key rate-limit policy, rate-limit reason code, `IsMachineSurface`, channel label. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1`, `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Grant census block; migration name list. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Accepted API-01 submission contract. |

## Not touched

`docs/current-architecture.md` / `docs/operations.md` (DELIV-030), `docs/capabilities.md` (TICK-061 moved API-01 to Now), Principal settings dialog (PLAT-050), any Worker file (the binding port is registered by Infrastructure DI and resolved by `ProcessIntake`'s optional parameter).
