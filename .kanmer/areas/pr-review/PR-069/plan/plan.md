# Plan — PR-069: Resynchronize Unidentified state after reversing a Case link

*The plan. Not the checklist — reasoning establishes bounded work; the checklist distils it into independently observable actions.*

**Plan sizing (diff estimate).** Eleven source files and eight test files.
Hand-written diff ≈ **760 added / 60 removed** lines, of which ≈ 300 are tests.
One migration adds a nullable column; its generated `.Designer.cs` and the model
snapshot add ≈ 7,600 further lines that are machine-written and reviewed as
generated artifacts. No new project, directory, DI registration, timer, queue,
route or package.

## Objective

An Unidentified item follows its origin receipt's **effective** destination for
the whole life of a reversible Case association: link resolves it, unlink
reopens it, relink retargets it — and the reconciliation queue that performs the
correction always advances, so the correction cannot silently stop happening.

## Starting state

Evidence: PR-069 `scratch/review`@`8d55a5c425d8c59a`,
PR-069 `scratch/work-pack-reconciliation`@`b4dc81a965a224eb`,
INTK-048 `scratch/review`@`3f51b4f13d8d53c8`, INTK-048 `files`@`519c99db4982d773`,
INTK-048 `plan`@`fc4abc464f6b27d8`, PR-069 `files`@this diff.
Code read at `origin/dev` `9b8f78a36151313bc6d48625edee7f13a2173127`
(worktree `C:\Users\PGUSER\Documents\github\pegasus-worktrees\pr-069-unidentified-link-reversal`,
branch `task/pr-069-unidentified-link-reversal`).

**Reproduction (smallest, on `origin/dev` today).** `UnidentifiedState` is
`Open | Resolved` only (`src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs:21-25`)
and `src/` contains no reopen path.

1. A receipt whose decision is `NeedsSorting` / `Unsupported` / `OcrRequired` /
   `TechnicalFailure` registers an open U-item
   (`src/Pegasus.Core/Intake/ProcessIntake.cs:282-294`).
2. Staff correct the intake. `EfIntakeMutationStore.ResolveAsync`'s correction
   branch rewrites the receipt to `IntakeDecision.CaseCreated`
   (`src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs:216-217`).
3. Staff link the receipt to Case A (`LinkAsync`, `:280-352`). `CurrentCaseId`
   now resolves to A (`src/Pegasus.Core/Intake/IntakeContracts.cs:445-446`).
4. The 10-second sweep (`src/Pegasus.Worker/IntakeFunctions.cs:193`) takes the
   `Decision == CaseCreated && CurrentCaseId is { }` branch
   (`src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs:110-115`) and
   resolves the U-item to Case A. `Resolved` is terminal.
5. Staff unlink (`ReverseLinkAsync`, `:354-402`): `association.IsActive = false`,
   `association.Version++`, and **no Unidentified owner is touched**.
   `EfIntakeReceiptStore.Map:561-562` gates `ManualLinkedCaseId` on `IsActive`
   but sets `ManualAssociationVersion` unconditionally, so `CurrentCaseId` is now
   `null`.

**Defect 1 — terminal resolution to a destination that no longer exists.** The
U-item stays `Resolved` against Case A with no effective destination: gone from
the open queue (`EfUnidentifiedStore.ListQueueAsync:245-270` filters
`State == Open`) and never revisited, because `ResolveForReceiptAsync` returns
early for a non-open item (`ReconcileUnidentifiedDestinations.cs:102-105`) and
`RegisterAsync` returns the existing resolved row on replay
(`EfUnidentifiedStore.cs:42-62`). A later relink to Case B leaves the recorded
destination pointing permanently at A. The retained material has no route and no
queue — the two-queues failure INTK-033 closed, inverted.

**Defect 2 — the correction queue must advance or it starves itself.** This is
the defect that refuted the first fix, recorded at PR-069 `scratch/review` and
reproduced against real SQL. Two facts bind the design:

- A destination change need not mutate the origin receipt (opening a Triage for
  a receipt already linked to a Case reads it `AsNoTracking`), so a
  receipt-keyed operation key
  (`ReconcileUnidentifiedDestinations.cs:155`, `intake-unidentified-reconcile:{receipt.Id}:{receipt.Version}`)
  is **not** unique per destination change. A reopen-then-re-resolve pair rebuilt
  the key the first resolve had already taken; `UnidentifiedHistory.OperationKey`
  is unique (`PegasusDbContext.cs:968`), so `ResolveAsync` rejected it as a
  conflicting replay, and every later sweep rebuilt the same taken key and failed
  the same way — permanently.
- A recheck that concludes "destination unchanged" writes nothing, so a
  freshness predicate comparing association timestamps against `ResolvedAtUtc`
  never advances. Those rows carry the oldest `ResolvedAtUtc`, so on a bounded
  oldest-first page they hold the head and starve every later stale resolution
  of its recheck, in silence. A timestamp watermark is also untestable here: the
  integration harness runs on a frozen `TimeProvider`.

**Regression boundary — what must not change.** `ReconcileUnidentifiedDestinations`
remains the sole automatic derivation of a receipt's Unidentified destination
(second-implementation check, INTK-048 `scratch/review`): teaching
`ReverseLinkAsync` or a Razor handler to decide Unidentified state is a stop
condition. Existing precedence is preserved exactly — `Decision == CaseCreated`
plus an effective Case → `InstructionCase`; `ImageIntakeRegistered` →
`ImageIntake`; a Triage request whose Triage exists → `Triage`; anything else →
no destination and the item stays open. A receipt that is still legitimately
unidentified is never force-closed. INTK-048's trailing "any `CurrentCaseId` is
a Case destination" branch is **not** introduced here.

## Governing docs

- **Meets `docs/frd/frd-02-intake-and-source-identity.md` § Unidentified
  destination and reference (`:33-45`).** The FRD fixes the state vocabulary at
  "Unidentified is open or resolved" and requires automatic resolution to a real
  destination by the product's own reconciliation, with the destination recorded
  in history and genuinely unidentified material never force-closed. This diff
  adds no state and no second reconciler: it makes the same owner apply the same
  rule when the effective destination *changes*, and appends the reopen and the
  re-resolve to the same immutable history.
- **Meets the same FRD § Matching conflicts and reversible association
  (`:367`).** "Any authorised staff member may reasonedly unlink or reassociate a
  mistaken match; the prior relationship and both source origins remain
  permanent, and dependent facts and counts recompute without deleting history."
  A U-item's resolved state is a dependent fact of the effective association, so
  recomputing it on unlink and relink is the FRD's requirement, not an extension
  of it. Nothing is deleted: reopen and re-resolve are appended history rows.
- **No governing document is modified and no new ADR is written.** The FRD does
  not name "reopen" as a transition, but it is implied rather than silent by the
  clause above, and the change creates no new boundary: same Core owner, same
  table, same two states, one added nullable column. AGENTS.md's ADR trigger (a
  new top-level directory, project, store, runtime, migration stream or
  deployment unit) is not met.

## Required changes

1. `IUnidentifiedStore` gains three members: `ReopenAsync` (a validated,
   replay-checked `Resolved -> Open` transition that clears the resolution
   fields), `ListResolutionsToRecheckAsync` (the freshness filter — a query, not
   a destination decision) and `MarkResolutionRecheckedAsync` (completes a
   recheck). `UnidentifiedState` is unchanged.
2. `ReconcileUnidentifiedDestinations` becomes the single synchronization rule.
   `ResolveForReceiptAsync` is renamed `SynchronizeForReceiptAsync`; destination
   derivation is factored into one private helper used by both the open branch
   and the recheck branch; a resolved item this reconciliation itself wrote is
   reopened when the effective destination is withdrawn and reopened-then-
   re-resolved when it changed. An item resolved by anyone else (staff, another
   actor) is never touched.
3. **Operation keys are keyed on the Unidentified item, not the receipt:**
   `intake-unidentified-{transition}:{item.Id:N}:{item.Version}`. The item's
   version is the expected version each transition applies at, which gives both
   required properties — a retry of the same logical transition rebuilds the same
   key and replays, while reopen at V and re-resolve at V+1 can never collide
   with the resolve at V-1 — and it holds even when the receipt is never
   mutated. This replaces the receipt-keyed form at
   `ReconcileUnidentifiedDestinations.cs:155`.
4. **Freshness is the manual association's own `Version`, never a timestamp.**
   `UnidentifiedItems.ReconciledAssociationVersion` (nullable `long`) records the
   association version a resolution's destination was last reconciled against.
   `ListResolutionsToRecheckAsync` selects rows this reconciliation resolved
   whose watermark is NULL or differs from the current association version;
   every completed recheck records the version **it read**, so the row leaves the
   queue and an association that moves mid-pass is picked up next pass instead of
   being marked reconciled unseen. `ReopenAsync` clears the watermark with the
   rest of the resolution. NULL means "resolved, never yet rechecked", which is
   what every existing row is — one pass each, no backfill.
5. The sweep reports a `Corrected` count alongside `Resolved` and `Failures`,
   logged by the existing `LoggerMessage` in `IntakeFunctions`.
6. `Details.cshtml.cs`'s advisory catch around the synchronizer stops swallowing
   `UnidentifiedOperationConflictException`. The sweep is a backstop only for
   faults it can retry; a permanently taken operation key is not one, so
   swallowing it reports a 302 success over lost work (rules 11 and 12). A
   version conflict stays advisory — it means the item moved under the write and
   the sweep re-reads it.

*No `investigate` / `decide` / `choose` / `determine` remains: the operation-key
form (item version), the freshness token (association version) and the
`Details.cshtml.cs` scope (in this ticket) are decided above.*

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` | Reopen request/result/validation and the two recheck-queue members on the existing port. |
| Modify | `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` | The one owner: synchronize, reopen/retarget, item-keyed operation keys, `Corrected`. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` | `ReopenAsync`, the real recheck predicate, the recheck completion write. |
| Modify | `src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs` | `ReconciledAssociationVersion` on `UnidentifiedItemEntity`. |
| Modify | `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Map the new column on the existing `UnidentifiedItems` configuration. |
| Add | `src/Pegasus.Infrastructure/Persistence/Migrations/*_UnidentifiedResolutionRecheckWatermark*.cs` | The column plus its Worker-role permission assertion; `.Designer.cs` is a generated artifact. |
| Modify | `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Generated snapshot for the new column. |
| Modify | `src/Pegasus.Core/Intake/DurableIntake.cs` | The processing pass calls the renamed method. |
| Modify | `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` | Renamed call, and the conflict class no longer swallowed. |
| Modify | `src/Pegasus.Worker/IntakeFunctions.cs` | Log the `Corrected` count on the existing sweep. |
| Modify | `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` | Core coverage of reopen, retarget, no-op and the key contract. |
| Modify | `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | Real-SQL lifecycle and the real recheck predicate. |
| Modify | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | The applied-migration census gains the new id. |
| Modify | `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs` | Fake store implements the new port members. |
| Modify | `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` | Fake store implements the new port members. |
| Modify | `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Fake store and the widened result record. |
| Modify | `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` | Fake store implements the new port members. |
| Modify | `tests/Pegasus.IntegrationTests/UploadOutcomeQueriesTests.cs` | Fake store implements the new port members. |

## Do not modify

- `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` — the
  association owner stays the association owner; it must not decide Unidentified
  state.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — the
  effective-association mapping at `:561-575` is correct as written.
- `src/Pegasus.Core/Intake/IntakeContracts.cs` — `CurrentCaseId` /
  `CurrentCaseReference` / `UnlinkCancelsCase` are the one derivation; reuse, do
  not copy or extend.
- `src/Pegasus.Core/Intake/ProcessIntake.cs` — eligibility, deferral and
  Triage-request rules are unchanged.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`,
  `src/Pegasus.Web/Pages/Unidentified/**`, `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs`
  and every `.cshtml` — no UI or markup change.
- `scripts/**` and `.github/workflows/**` — unless the migration genuinely trips
  the grant-carrying census (see Constraints); that is a deviation to report, not
  a silent edit.
- `AGENTS.md`, `docs/**` — no command, convention or governing-document change
  follows from this diff.
- `.worktrees/kanmer`, the branch `kanmer-board`, and the primary checkout
  `C:\Users\PGUSER\Documents\github\pegasus`.

## Constraints

- **One Core owner.** `Pegasus.Core` owns the rule; Infrastructure supplies the
  transition and the query. A second state derivation anywhere is a stop
  condition (AGENTS.md architecture invariants, rule 7).
- **Replay stability is not traded away.** Every transition keeps an operation
  key that a genuine retry rebuilds identically; uniqueness comes from the item's
  version moving, not from a clock, a GUID or a counter.
- **No timestamp comparison decides freshness.** The integration harness runs on
  a frozen `TimeProvider`; a timestamp watermark would look green while broken.
- **The recheck predicate gets a real-persistence test.** The Core fake returns a
  hand-populated list and cannot see the predicate at all — that is exactly how
  defect 2 shipped green.
- **Schema change ships whole (rule 16).** The nullable column, the Worker-role
  permission assertion, and the committed applied-migration census ride this
  diff. `scripts/Test-MigrationGrants.ps1` requires a grant only for tables
  created by `Up()`, so a column needs none. `scripts/Test-AzureDeploymentPlan.ps1:300-313`
  scans post-baseline migrations for a case-sensitive `\bGRANT\s` and then
  demands the migration id in `Invoke-AzureDatabaseBootstrap.ps1`: write the
  assertion and its comments **without** the literal token `GRANT ` (query
  `sys.database_permissions` for `permission_name = N'UPDATE'`), and if the check
  still trips, stop and report rather than editing the bootstrap script to claim
  a grant that is not issued.
- **One unmerged migration at a time** (programme practice for this run: two
  branches adding migrations conflict on the model snapshot). The last migration
  on `dev` is `20260829212237_GrantProviderSubmissionAcceptRecovery`. Confirm no
  other in-flight branch carries one before generating this migration; if one
  does, stop and report.
- **Assertions are never weakened (rule 19).** The expected operation keys in
  `ReconcileUnidentifiedDestinationsTests` stay interpolated from the item's own
  id and version. A hard-coded literal version — the exact weakening the refuted
  round 3 introduced — is a stop condition.
- **No new dependency, package, DI registration, timer, schedule or route.**
- Corpus fixtures are read-only; no fabricated domain data — tests drive the real
  upload, correct, link and unlink ports.

## Ordered steps

### Step 1 — Extend the Unidentified port with the reopen transition and the recheck queue
- Preconditions: worktree assertions pass; `git -C <worktree> status --porcelain` is clean at `origin/dev` `9b8f78a3`.
- Files: `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs`, `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs`, `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`, `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`, `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`, `tests/Pegasus.IntegrationTests/UploadOutcomeQueriesTests.cs`
- Reuses: the existing `IUnidentifiedStore` port and `UnidentifiedValidation` helpers (`RequireStaffOrAutomation`, `RequireOperation`, `RequireText`, `RequireUtc`) — no new validation vocabulary.
- Change: add `ReopenUnidentifiedRequest`, `UnidentifiedReopenResult`, `ValidateReopen`, and the three interface members (`ReopenAsync`, `ListResolutionsToRecheckAsync`, `MarkResolutionRecheckedAsync`) with XML docs stating that the queue is a freshness filter and never a destination decision. Implement the three members on the five test fakes: the reopen throws `NotSupportedException`, the queue returns an empty list, and the mark is a no-op, except in `ReconcileUnidentifiedDestinationsTests`'s `FakeUnidentifiedStore`, which records reopen calls and the marked versions for the Core tests in Step 5.
- Preserved behaviour: `UnidentifiedState` keeps exactly `Open` and `Resolved`; every existing member signature is unchanged.
- Forbidden: a third state; a second store port; changing `RegisterAsync`/`ResolveAsync` semantics.
- Negative cases: `ValidateReopen` refuses an empty item id, a negative expected version, a non-staff/non-automation actor, a blank or over-long reason, and a non-UTC timestamp.
- Tests: none of their own — the contract is proved by Steps 5 and 6.
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: exit 0, 0 warnings, 0 errors.
- Done when: the solution compiles with the widened port and every fake implements it.
- Deviation stop: a sixth `IUnidentifiedStore` implementation appears that the plan did not name.

### Step 2 — Implement the transition, the predicate and the completion write in persistence
- Preconditions: Step 1 committed.
- Files: `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs`, `src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs`, `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, `src/Pegasus.Infrastructure/Persistence/Migrations/*_UnidentifiedResolutionRecheckWatermark*.cs`, `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
- Reuses: `EfUnidentifiedStore.ResolveAsync`'s exact shape (`:138-203`) — serializable transaction, operation-key replay check, version/state guard, `Version++`, one appended `UnidentifiedHistoryEntity`, `Map` for the result — and the existing `UnidentifiedItems` model configuration and `IntakeManualAssociationEntity`.
- Change: `ReopenAsync` mirrors `ResolveAsync` in reverse (guard `State == Resolved` and `Version == ExpectedVersion`; clear `ResolvedAtUtc`, the resolved-by actor fields, the resolution reason and target fields, and `ReconciledAssociationVersion`; append a `Resolved -> Open` history row). Its replay branch must return the item state **the replayed reopen produced**, not whatever the row currently holds. `ListResolutionsToRecheckAsync` joins `UnidentifiedItems` to `IntakeManualAssociations` on the origin receipt id and selects `State == Resolved`, resolved by this reconciliation's automation identity, `OriginKind == Receipt`, and `ReconciledAssociationVersion == null || ReconciledAssociationVersion != association.Version`, ordered `ResolvedAtUtc, Sequence`, `Take(maximum)`. `MarkResolutionRecheckedAsync` writes the observed association version with `ExecuteUpdateAsync` scoped to `Id` **and** `State == Resolved`, without the concurrency token — it is bookkeeping about a resolution, not a transition, so it must not resurrect a watermark onto a row that has since been reopened. The migration adds the nullable column and asserts, guarded on the SQL Server provider, that the Worker runtime role still holds object-level `UPDATE` on `dbo.UnidentifiedItems`, throwing on drift; `Down` drops the column.
- Preserved behaviour: `RegisterAsync`, `ResolveAsync`, both probes, `GetByOriginAsync`, `ListAsync`, `ListQueueAsync` and `HistoryAsync` are untouched; the automation subject id exists in one place and persistence reads it rather than repeating the literal.
- Forbidden: deleting or rewriting any history row; a non-serializable reopen; `ExecuteUpdateAsync` unscoped by state; a new index or table; the literal token `GRANT ` in the migration file.
- Negative cases: reopening an `Open` item or at a stale version throws `UnidentifiedVersionConflictException`; a replayed reopen whose recorded row disagrees throws `UnidentifiedOperationConflictException`; a mark against a reopened row updates nothing.
- Tests: `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` (Step 6).
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: exit 0; `dotnet ef migrations list` is not required — the census test in Step 6 is the evidence.
- Done when: the store implements the three members and the migration and snapshot are committed together.
- Deviation stop: the design needs a second column, an index, or any change to an existing column's type or nullability.

### Step 3 — Make `ReconcileUnidentifiedDestinations` the one synchronization rule
- Preconditions: Steps 1-2 committed.
- Files: `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs`
- Reuses: the existing owner and its existing precedence chain; `IntakeReceipt.CurrentCaseId` / `CurrentCaseReference` for the effective association; `ProcessIntake.IsUnidentifiedEligible` / `IsTriageRequest`; `IntakeExceptionPolicy.IsRecoverable` for the sweep's per-item isolation.
- Change: rename `ResolveForReceiptAsync` to `SynchronizeForReceiptAsync` and keep it the only public per-receipt entry point. Factor the destination chain into one private `DestinationForAsync` returning a small private record, with the **existing** conditions unchanged (`Decision == CaseCreated` plus an effective Case → `InstructionCase` using `AcceptedCaseReference ?? ManualLinkedCaseReference`; `ImageIntakeRegistered` → `ImageIntake`; Triage request with a Triage → `Triage`; otherwise none). For an `Open` item, behave exactly as today. For a `Resolved` item, act only when this reconciliation's own automation actor wrote it and the effective destination differs from the recorded one: reopen, and re-resolve when a destination exists. Add a second bounded loop over `ListResolutionsToRecheckAsync(maximumItems)` that counts `Corrected`, and call `MarkResolutionRecheckedAsync` with the association version that pass read for every candidate it completed, whether or not it corrected anything. Build both operation keys as `intake-unidentified-{transition}:{item.Id:N}:{item.Version}` in one private helper, documenting why the item's version and not the receipt's. Expose the automation subject id as a constant so persistence has one source for it.
- Preserved behaviour: an item resolved by staff is never reopened; a genuinely unidentified receipt is never force-closed; a group-origin item is still skipped; a per-item failure still increments `Failures` and never stops the sweep; INTK-048's trailing `CurrentCaseId` branch is **not** added.
- Forbidden: a receipt-keyed operation key; a clock, GUID or counter in a key; deriving Unidentified state anywhere but here; changing the existing precedence order.
- Negative cases: an item whose recorded destination still matches is not written to (no reopen, no resolve) and only its watermark advances; a resolved item with a non-automation resolver is untouched.
- Tests: `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` (Step 5).
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: exit 0, 0 warnings, 0 errors (the two callers still compile against the old name until Step 4 — expect and fix those two errors in Step 4, or rename and update callers in one commit).
- Done when: the owner reopens, retargets and marks, with item-keyed operation keys.
- Deviation stop: the rule cannot be expressed without a second derivation or without touching the association owner.

### Step 4 — Update the three composition-root callers
- Preconditions: Step 3 committed.
- Files: `src/Pegasus.Core/Intake/DurableIntake.cs`, `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs`, `src/Pegasus.Worker/IntakeFunctions.cs`
- Reuses: the existing advisory call site in the processing pass (`DurableIntake.cs:911`), the existing `CloseUnidentifiedForTriageAsync` hook (`Details.cshtml.cs:653-665`) and the existing `LogUnidentifiedDestinationReconciliation` `LoggerMessage` (`IntakeFunctions.cs:274-275`).
- Change: call the renamed method from both callers. Narrow the page handler's catch filter to `exception is not UnidentifiedOperationConflictException && IntakeExceptionPolicy.IsRecoverable(exception)`, with a remark saying why the conflict class is excluded and why a version conflict stays advisory. Add `Corrected` to the sweep's log call and message template.
- Preserved behaviour: the sweep remains on the existing timer with no new schedule; the page's advisory-write intent is unchanged for retryable faults; no route, handler or markup changes.
- Forbidden: a catch-all suppression; calling the synchronizer from `ReverseLinkAsync`, the unlink handler or `Mail/Message.cshtml.cs`; a new log event or metric.
- Negative cases: an operation-key conflict raised by the synchronizer now surfaces from the Open-the-Triage POST rather than returning 302.
- Tests: covered by Steps 5-6 plus the existing Web tests that must stay green.
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: exit 0, 0 warnings, 0 errors.
- Done when: the whole solution builds and `ResolveForReceiptAsync` no longer exists.
- Deviation stop: a caller outside these three files references the renamed method.

### Step 5 — Core tests for the synchronization rule
- Preconditions: Steps 1-4 committed and building.
- Files: `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs`
- Reuses: the existing `Harness`, `FakeUnidentifiedStore`, `FakeResolveUnidentified`, `FakeReceiptQueries`, `FakeTriageQueries` and `FakeImageIntakeQueries` in the same file — no new harness.
- Change: add tests that a resolved item whose effective Case was withdrawn is reopened and not re-resolved; that a resolved item whose effective Case changed is reopened and re-resolved to the new Case with the new reference; that an unchanged destination writes no transition but still marks the recheck complete; that a staff-resolved item is never reopened; and that the reopen and re-resolve keys are `intake-unidentified-reopen:{item.Id:N}:{item.Version}` and `intake-unidentified-reconcile:{item.Id:N}:{item.Version}` built from the item under test. Update the existing expected keys at `:36` and `:106` to the item-keyed form, interpolated.
- Preserved behaviour: all nine existing tests keep their claims; `PromotedImageReceiptResolvesItsOpenItemToTheImageIntake` keeps an interpolated expected key.
- Forbidden: a literal version in any expected key; deleting or relaxing an existing assertion; asserting the recheck predicate against the fake (it cannot see it).
- Negative cases: the withdrawn-destination test asserts no resolve call followed the reopen; the staff-resolved test asserts zero store writes.
- Tests: this file.
- Commands: the test runner runs `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReconcileUnidentifiedDestinationsTests"`.
- Expected output: every test in the class passes; no skips.
- Done when: the class covers reopen, retarget, no-op, staff-owned and key format.
- Deviation stop: a claim cannot be expressed without weakening an existing assertion.

### Step 6 — Real-SQL lifecycle and recheck-predicate tests, and the migration census
- Preconditions: Step 5 committed.
- Files: `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs`, `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
- Reuses: `IntakeWebApplicationFactory`, `IntakeWebDriver.UploadAndProcessAsync`, the DI-resolved `IUnidentifiedStore`, `ReconcileUnidentifiedDestinations`, `IResolveIntake`, `ILinkIntake` and `IReverseIntakeLink`, and the `[Trait("Category", "SqlServer")]` shape of the existing tests in the file.
- Change: add one test that drives the whole lifecycle through production ports — upload and process to an open U-item, correct the intake so the decision becomes `CaseCreated`, link Case A, sweep (resolved to A), unlink, sweep (reopened, no destination, back in the open queue), relink to Case B, sweep (resolved to B) — asserting item state, target kind, target id and reference, the appended history at each step, and that a further sweep returns all zeros with a single queue. Add one test that pins the real predicate: a completed recheck stops qualifying (call `ListResolutionsToRecheckAsync` directly before and after), and with `maximum: 1` a later stale resolution reaches the head of the page once the first row is completed. Add the new migration id to the applied-migration census.
- Preserved behaviour: both existing tests in the file keep their assertions; the frozen `TimeProvider` is not unfrozen to make anything pass.
- Forbidden: seeding rows with raw SQL or a DbContext write in place of a production port; asserting on `ResolvedAtUtc` ordering as a proxy for freshness; skipping a test when SQL Server is unavailable (a non-PASS is recorded honestly).
- Negative cases: the second sweep proves zero failures and zero corrections — a stuck queue would show a non-zero `Candidates`/`Failures` forever.
- Tests: this file.
- Commands: the test runner runs `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UnidentifiedReconciliationTests"` and the migration census test, where SQL Server exists; otherwise CI `sql-integration` at the PR head is the evidence.
- Expected output: all tests in both classes pass; the census test reports no pending migrations.
- Done when: the lifecycle and the predicate are both proved against real persistence.
- Deviation stop: the lifecycle cannot be built from production ports on `origin/dev` — report the exact port that refuses, and do not import INTK-048's change to make it reachable.

### Step 7 — Simplification pass, report and hand-off
- Preconditions: Steps 1-6 committed; the solution builds Release-clean.
- Files: no repository files beyond fixes the pass itself applies within the Expected files list.
- Reuses: `/simplify` plus an independent lens over this branch's own diff.
- Change: record findings and dispositions under the dated `## Simplification pass` heading below (`set_ticket_doc doc: "plan"`), apply only behaviour-preserving fixes, then write the post-implementation report.
- Preserved behaviour: every acceptance check still holds after any applied finding.
- Forbidden: silencing a finding; a behaviour change smuggled in as simplification.
- Negative cases: an unapplied finding must be named with a reason or a ticket (rule 22).
- Tests: re-run the focused commands after any applied change.
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: exit 0.
- Done when: the plan carries the dated pass with honest dispositions and the report is written.
- Deviation stop: a finding cannot be applied without leaving the plan's scope.

## Acceptance checks

- **Named production callers.** The reopen/retarget rule is reached from
  `src/Pegasus.Worker/IntakeFunctions.cs:193` (the existing 10-second sweep) and
  from `src/Pegasus.Core/Intake/DurableIntake.cs:911` (the receipt's own
  processing pass); `ReopenAsync`, `ListResolutionsToRecheckAsync` and
  `MarkResolutionRecheckedAsync` are called only from
  `ReconcileUnidentifiedDestinations`, which is registered at
  `src/Pegasus.Infrastructure/DependencyInjection.cs:120`. Nothing is
  registered-but-unreachable (rule 14).
- **Schema change complete (rule 16).** The nullable column, the Worker-role
  `UPDATE` assertion on `dbo.UnidentifiedItems`, the generated snapshot, and the
  committed applied-migration census ride this diff; `Down` drops the column and
  no data is lost by a rollback (NULL is the pre-migration meaning).
- **Reversal.** After link → sweep → unlink → sweep, the U-item is `Open`, has no
  resolution target, and is back in the Unidentified queue exactly once.
- **Relink.** After unlink → relink to Case B → sweep, the U-item is `Resolved`
  to Case B with B's reference, and Case A survives only as history.
- **Steady state.** A further sweep over the same data returns
  `Candidates = 0`, `Resolved = 0`, `Corrected = 0`, `Failures = 0`, and a
  completed recheck no longer appears in `ListResolutionsToRecheckAsync`.
- **Idempotence.** Repeating any single transition rebuilds the same operation
  key and replays; reopen and re-resolve never share a key.
- **Errors surface.** An operation-key conflict from the Open-the-Triage POST is
  no longer swallowed into a 302.
- **No weakened assertions (rule 19).** Every expected operation key in the tests
  is interpolated; no existing assertion is deleted or relaxed.
- **One owner.** `ReconcileUnidentifiedDestinations` remains the sole automatic
  derivation of a receipt's Unidentified destination.
- Exact commands and exit codes are retained for every run (rule 20);
  INCONCLUSIVE is not PASS.

## Commands

Implementer, in `C:\Users\PGUSER\Documents\github\pegasus-worktrees\pr-069-unidentified-link-reversal`
(compiler feedback only — the implementer never runs test suites):

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

Test runner, same worktree. This workstation has **no SQL Server LocalDB**, so
only these lanes can run locally:

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReconcileUnidentifiedDestinationsTests"
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
pwsh -File ./scripts/Test-MigrationGrants.ps1
pwsh -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
```

CI `repository-check` at the PR head is the evidence for everything that needs a
database or a browser. These jobs must be green before the PR opens:

- `changes` — migration runtime-grant check and the Local Azure deployment plan
  (the grant-carrying migration census).
- `unit` — Core and Architecture suites.
- `sql-integration` (all shards) and `sql-integration-coverage` — the real-SQL
  lifecycle, the recheck predicate and the applied-migration census.
- `browser`, `test-ui`, `documentation`, `infrastructure`,
  `local-development-scripts`, `reference-data` — unchanged surfaces, still green.

Post-merge proof reruns the focused Core and SQL reconciliation tests at the
merged SHA. Production verification of live rows requires a separately approved
release and is not part of this ticket.

## Failure and deviation rules

Stop and report — do not improvise — on: a failing build or test that the plan
does not predict; a file outside Expected files needing a change; a sixth
`IUnidentifiedStore` implementation; the design needing a second column, an
index, or a change to an existing column; another in-flight migration; the
grant-carrying census still tripping after the wording fix; an assertion that
would have to be weakened; the lifecycle test being unbuildable from production
ports on `origin/dev`; any conflict with FRD-02 or AGENTS.md; or a command that
would push, merge, rebase, force, or write outside this worktree. Record the
exact command, cwd, exit code and result for every run; a first failure is kept
even when a retry passes. Deviations are reported, never silent redesigns.

## Simplification pass — <YYYY-MM-DD>

*To be filled by the implementer before the PR opens, over this branch's own
diff: reuse/duplication, simplification, efficiency and abstraction altitude.
Name every finding and its disposition — applied, rejected with a reason, risk
accepted, or deferred to a named ticket — and state "no unapplied findings" only
when that is true (AGENTS.md workflow step 4, rule 22).*

## Stop condition

Implementer: after the build-green commits on
`task/pr-069-unidentified-link-reversal`, report **READY_FOR_TESTS** and stop —
do not run the test suites. Then, once the test runner has recorded PASS and CI
`repository-check` is green at the PR head, open the PR to `dev` titled
"Resynchronize Unidentified state after reversing a Case link (PR-069)" with the
footer `Kanmer: PR-069`, record `commits` and `prs` with `update_item`, and move
the ticket from implementing to review. Do not merge the PR, do not promote to
`main`, do not touch INTK-048, do not start or take another ticket, and do not
dispatch.
