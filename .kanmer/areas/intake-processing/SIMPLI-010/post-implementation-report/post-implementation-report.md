# Post-implementation report — SIMPLI-010

Branch `task/simpli-010-intake-state` @ `1e5372ce` on `dev` `fc144848`. PR #387. Diff: 22 files, +33/−62 (5 src, 12 fixture test files / 14 sites, 2 rename-only test files, 3 docs).

## What changed, file by file

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Deleted `DecisionCodes` and the `ParseDecision` `"draft_ready" => CaseCreated` branch; the `ListAsync` decision filter compares `item.Decision == ToCode(requested)`; comment no longer mentions the alias. | Plan step 2 — the alias is the only reason the filter matched two codes. `ToCode` is total over `IntakeDecision`, so the single-code filter is equivalent; unknown persisted codes still throw. |
| `src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs` | `MapIntakeState` succeeded-set no longer lists `draft_ready`. | Same. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | Removed the "kept readable" legacy comment. | Same. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Replaced the `DraftReady`/`draft_ready` read-compatibility paragraph with two sentences: `CaseCreated` is a processing decision, not proof a Case exists; the allocation/link projection alone says whether one does. | Plan step 2 (retain the concise current rule). |
| `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` | Removed the `"draft ready" or "instruction draft"` tone arm — none of the partial's 20 callers passes either string; the live label is "Ready for case allocation" and intake decisions do not reach the chip. | Simplification pass — dead vocabulary. |
| Fixture tests (12 files, 14 sites): `AssessmentPersistenceIntegrationTests`, `CaseDataCompletenessPersistenceTests`, `CaseMatchIntegrationTests`, `CaseTaskArchivePersistenceTests`, `CaseWorkflowMigrationTests` (×2), `CaseWorkflowPersistenceTests`, `ConcurrencyTokenPersistenceTests`, `EvaHandoffPersistenceTests`, `ProviderInspectionModeAcceptanceTests`, `TypedCaseDataMigrationTests` (×2), `VehicleWorkflowTerminalTests`, `DocumentCustodyDurabilityTests` (`"DraftReady"` → `"case_created"`, inert — never parsed by the tested path) | Incidental seed values → `case_created`; no assertion in those files reads `Decision`. | Plan step 3. |
| `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs`, `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Three test methods renamed (`…DraftReady…` → `…CaseCreated…`); bodies unchanged and still assert `IntakeDecision.CaseCreated` / `NeedsSorting`. | Acceptance criterion: no `DraftReady` occurrence in active tests. |
| `docs/design/README.md` | The two complete-state contract rows (now `:650`/`:893`) name "Ready for case allocation" instead of "Draft ready" (matches the label table at `:418`, `Intake/Details.cshtml.cs:360`, `Mail/Message.cshtml.cs:129`); the `DraftReady` paragraph replaced by a `CaseCreated` sentence; a duplicated opener and a dangling read-compatibility sentence removed. | Plan step 4 + simplification pass. |
| `docs/current-architecture.md` | "A `case_created` decision is not case-existence authority." | Plan step 4. |
| `CONTEXT.md` | Deleted the last repo-wide `DraftReady` sentence. | Simplification pass. |

## Deviations from the plan

- The plan's `rg` pattern (`draft_ready|DraftReady`) missed "Draft ready" in the design contract rows and CONTEXT.md; the pass widened it to case-insensitive `draft.?ready`. Imported design sources (`docs/design/system/`, `.design-sync/`, `design/`) are outside the ticket and still carry the old word.
- The stale-`dispatched` re-dispatch item added to this plan by the SIMPLI-009 review was **not** implemented here: the read-only production count found 0 such rows, so it is resilience, not repair, and is filed as [[INTK-003]] rather than folded into an alias-removal change. Retry/recovery state has one source already (`IntakeAllocationAttempts` → `IntakeAllocationProjectionStatus`); the `dispatched` gap is a work-item liveness hole, not a second truth.
- No production-data normalisation, migration, or repair — the count made the "Correction" premise a fact.

## Verification on `1e5372ce`

- `dotnet restore ./Pegasus.slnx --locked-mode`; `dotnet build ./Pegasus.slnx --configuration Release --no-restore`: 0 warnings, 0 errors.
- `Pegasus.Core.Tests`: 572 passed. `Pegasus.ArchitectureTests`: 94 passed.
- `Pegasus.IntegrationTests` filter (IntakeStablePersistence, QdosIntakeWeb, OperationsPersistence, CaseWorkflowMigration, TypedCaseDataMigration, Recovery, QdosTriage, CaseMatch, ConcurrencyToken, DocumentCustodyDurability, MailWorkspaceWeb, IntakeMcp): 69 passed, 6 skipped, 0 failed (1m58s). Seven of the twelve fixture files are exercised only by CI's full SqlServer lane; the edit there is a seed value no assertion reads.
- `rg -n -i "draft.?ready" src tests docs CONTEXT.md --glob '!docs/design/system/**'`: no matches; migrations, `scripts/`, `infra/`: no matches. `git diff --check`: clean.
- Read-only production check (2026-08-17): `IntakeReceipts WHERE Decision='draft_ready'` = 0 (research).

## Not claimed

No deployment or cloud write.
