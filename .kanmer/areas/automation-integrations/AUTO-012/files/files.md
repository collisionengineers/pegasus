# Files — AUTO-012

## Changed

| Path | Change | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/ProviderApi/ReconcileProviderSubmissions.cs` | New. A sixth bounded sweep that completes an incomplete accept record | A sweep that silently repairs nothing looks identical to a healthy one |
| `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs` | Derives the `Accepted` row's id from the operation key; literal operation key replaced by the policy helper | The idempotent id is what refuses a duplicate; it must be derived identically on both sides |
| `src/Pegasus.Infrastructure/Persistence/EfProviderSubmissionStore.cs` | Candidate query inner-joins the staged receipt so a bare reservation is unselectable | The join depends on a case-insensitive collation |
| `src/Pegasus.Infrastructure/Persistence/EfIdentityAuditStore.cs` | `TryAppendAsync` returns false on PK collision rather than throwing | Must not change `AppendAsync` for any other caller |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Runs the sweep and logs `FirstFailure` | Adds a dependency to a function carrying five other live sweeps |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Composes `IActionHistoryWriter` in the Worker | It was **never composed there before** — real wiring, not a registration claim |
| `…/Migrations/20260829212237_GrantProviderSubmissionAcceptRecovery.cs` | Grants the Worker `UPDATE` on `ProviderSubmissions` | Grants-only, no schema change |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Records the new grant in the census, and corrects the neighbouring comment | The census is an **equality** gate both ways |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/current-architecture.md` | Record the recovery behaviour | |

## Tests

| Path | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | Starvation regression (60 bare reservations + 1 repairable); a second `Accepted` row is refused by the database |
| `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs` | Core mirrors: never selects a bare reservation, loses the race cleanly, honest timestamp, exact failure string |
| `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Constructor list and structured-log field block **extended**, both still exact |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Migration ledger appended; the case-insensitive collation the joins depend on is pinned |

## Notably NOT changed

`IIntakeSubmission.ExecuteAsync`, `IIntakeWorkStore.ReceiveAsync`,
`ProcessIntake` and `DurableIntake` have **zero diff lines** — verified by the
reviewer. No new schedule, timer, queue, function, table or deployment unit, so
the seven-function Worker census is unchanged.

`EfIntakeWorkStore.ToCode` was briefly widened to `internal` to avoid a seventh
copy of the channel-code map, then reverted (`3077f887`) to keep the shared
intake path at zero diff lines. `EfProviderSubmissionStore` declares its own
`provider_api` constant instead, and the two SQL-level tests hold the two in
agreement — they find no candidate at all if they ever disagree.
