# Files — PR-069: Resynchronize Unidentified state after reversing a Case link

*The surface area of the change on `origin/dev` `9b8f78a3`. Written against the
code in `C:\Users\PGUSER\Documents\github\pegasus-worktrees\pr-069-unidentified-link-reversal`.*

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` | Add the `Resolved -> Open` transition to the existing port: `ReopenUnidentifiedRequest`, `UnidentifiedReopenResult`, `IUnidentifiedStore.ReopenAsync`, `ListResolutionsToRecheckAsync`, `MarkResolutionRecheckedAsync`, and `UnidentifiedValidation.ValidateReopen`. `UnidentifiedState` keeps its two values (`:21-25`) — no new state vocabulary. |
| `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` | The one Core owner of the supersession rule becomes the one owner of the *synchronization* rule: `ResolveForReceiptAsync` → `SynchronizeForReceiptAsync`, destination derivation factored out once, reopen/retarget for a resolved item whose effective destination changed, operation keys rebuilt from the **item's** identity and version, and a `Corrected` count on `ReconcileUnidentifiedDestinationsResult`. |
| `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` | `ReopenAsync` (serializable, replay-checked, clears the resolution fields and the recheck watermark), `ListResolutionsToRecheckAsync` (the real freshness predicate), `MarkResolutionRecheckedAsync` (completes a recheck so the queue advances). |
| `src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs` | `UnidentifiedItemEntity.ReconciledAssociationVersion` (nullable `long`): the manual-association version a resolution's destination was last reconciled against. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Map the new column on the existing `UnidentifiedItems` builder (`:914-945`). No new index is needed — the recheck query is bounded by `State` + the existing `(State, CreatedAtUtc, Sequence)` index and a `Take(50)`. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<stamp>_UnidentifiedResolutionRecheckWatermark.cs` | `AddColumn` for the new nullable column, plus a guarded `sys.database_permissions` assertion that the Worker role still holds object-level `UPDATE` on `dbo.UnidentifiedItems` (the sweep writes the column). New file; `.Designer.cs` and the model snapshot are generated with it. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Generated snapshot update for the new column. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | `:911` — the receipt's own processing pass calls the renamed method. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` | `:659` calls the renamed method, and `:661`'s `IntakeExceptionPolicy.IsRecoverable` catch-all is narrowed to exclude `UnidentifiedOperationConflictException` (rules 11 and 12: a permanently taken operation key is not a fault the sweep can retry, so swallowing it returns 302 over lost work). No markup change. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | `:193-198` and `:274-275` — log the new `Corrected` count on the existing sweep and its existing `LoggerMessage`. No new timer, schedule or function. |
| `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` | Reopen-on-withdrawn-destination, retarget-to-another-Case, unchanged-destination no-op, and the operation-key contract. The existing key assertions at `:36` and `:106` change to the item-keyed form and stay interpolated. |
| `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | The real-SQL lifecycle: correct → link → sweep resolves → unlink → sweep reopens → relink → sweep retargets, plus the steady state (a second sweep is all zeros) and the real recheck predicate (a completed recheck stops qualifying, and a later stale resolution reaches the head of a one-row page). |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | `:95-119` — the committed applied-migration census gains the new migration id. |
| `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs` | `:385` `FakeUnidentified` implements the three new `IUnidentifiedStore` members. Compile-only. |
| `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` | `:453` `StubUnidentifiedQueue` — same. Compile-only. |
| `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | `:366` `EmptyUnidentifiedStore` — same, plus the `Corrected` field on the result record. Compile-only. |
| `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` | `:980` fake store — same. Compile-only. |
| `tests/Pegasus.IntegrationTests/UploadOutcomeQueriesTests.cs` | `:483` fake store — same. Compile-only. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | `:445-446` `CurrentCaseId` is `ManualAssociationVersion is null ? AcceptedCaseId : ManualLinkedCaseId`, and `:470-473` `CurrentCaseReference` — the one effective-association derivation; never re-derive it. `:590-595` `IntakeExceptionPolicy.IsRecoverable` excludes only cancellation/OOM/AV, so it is a catch-all in practice. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | `:561-562` gates `ManualLinkedCaseId` on `IsActive` but sets `ManualAssociationVersion` **unconditionally** — which is why a reversed link yields `CurrentCaseId == null` while still exposing a monotonic association version to key freshness on. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | `LinkAsync` `:311-348` and `ReverseLinkAsync` `:354-402` both bump `association.Version` (`:335`, `:382`); `ExecuteAsync` `:731` bumps `receipt.Version` for both. `:216-217` `ResolveAsync`/correction rewrites `receipt.Decision` to `CaseCreated` — the transition that makes this defect reachable on `dev`. `:390-398` INTK-029 cancel-on-source-unlink. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | `:311-316` `IsUnidentifiedEligible`, `:326-330` `IsDeferredForAutomation`, `:349-351` `IsTriageRequest`, `:353-359` the registration operation key. These decide which receipts ever carry an open U-item. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | `:939` `Version` is the concurrency token; `:943` `RegistrationOperationKey` and `:968` history `OperationKey` are **unique** — which is exactly how a re-used operation key becomes a permanent failure. |
| `docs/frd/frd-02-intake-and-source-identity.md` | § Unidentified destination and reference (`:21-45`): two states, automatic resolution to a real destination, never force-closing genuinely unidentified material. § Matching conflicts and reversible association (`:367`): unlink/reassociate is supported and "dependent facts and counts recompute without deleting history" — the governing basis for reopening. |
| `AGENTS.md` | Rules 7 (name the owner you extend), 11 (a swallowed conflict is data loss), 12 (errors surface), 16 (schema change ships with its permissions and bootstrap census), 19 (never weaken an assertion), 22 (dispositions), and the dated `Simplification pass` requirement in the Repository task workflow. |
| `scripts/Test-AzureDeploymentPlan.ps1` | `:300-313` scans every post-baseline migration for a case-sensitive `\bGRANT\s` and then demands the migration id appear in `Invoke-AzureDatabaseBootstrap.ps1`. A migration that only *asserts* a permission trips it if it contains the literal token. |
| `scripts/Test-MigrationGrants.ps1` | `:7-19` only tables created by `Up()` need a grant; a new column on an existing table needs none. |
| `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | `:23-89` the existing sweep test is the pattern to follow: real upload through `IntakeWebDriver`, real ports from the DI scope, assertions on item state, target, history and replay. |
| Branch `origin/task/intk-048-unidentified-manual-link` (`b5fd8725`, `0147af6b`, `054bfe08`, `1f036337`, `51e7306c`) | The refuted first attempt and its round-3 repair. Read for what **not** to repeat (receipt-keyed reopen/re-resolve; a timestamp recheck predicate; a hard-coded expected key) and for the two ideas worth keeping (item-version keys; association-version freshness). Never cherry-pick: it also carries INTK-048's own change. |

## Ripple effects

- `ReconcileUnidentifiedDestinationsResult` gains a field, so every construction
  and equality assertion of it moves: `src/Pegasus.Worker/IntakeFunctions.cs`,
  `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`,
  `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs:88`.
- `IUnidentifiedStore` gains three members, so all five test fakes must implement
  them; the single production implementation is `EfUnidentifiedStore`.
- The new column is written by the Worker sweep, so the migration carries the
  Worker-role permission assertion in the same diff (rule 16), and the applied
  migration census in `IntakePersistenceIntegrationTests` must name it.
- No DI registration, timer, schedule, queue, public API, MCP tool or operator
  surface changes. `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs`,
  `src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs` and the Queues page read
  through unchanged members.

## Out of scope

- **INTK-048's own change is not re-applied here.** `ReconcileUnidentifiedDestinations`
  keeps `receipt.Decision == IntakeDecision.CaseCreated && receipt.CurrentCaseId`
  as the InstructionCase condition (`:110`). The trailing "any effective
  `CurrentCaseId` is a Case destination" branch, and the precedence change it
  implies, belong to INTK-048, which lands separately after this merges.
- No UI markup, no Razor view changes, no new page or handler.
- The `UnifiedWorkFunction` SQL deadlock, Azure Resource Health registration and
  any manual SQL repair of live rows.
- The broader "log the swallowed conflict's cause" work is [[INTK-053]]; this
  ticket only stops the one conflict class it introduces from being swallowed.
- Destination changes that are not manual-association changes (a Triage
  registration correction, an Image-intake withdrawal) are not added to the
  recheck queue: the receipt's own processing pass already calls the same owner.
- No backfill of existing resolved rows: a NULL watermark means "never yet
  rechecked", so each existing row gets exactly one recheck pass.
