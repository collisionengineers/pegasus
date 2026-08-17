# Impact — SIMPLI-010

This is a direct removal task. Pegasus has no retained Case/application data that requires `draft_ready` compatibility. No production inspection, data repair, normalization migration, deployment operation, or live-system evidence is part of the change.

| File / module | Change | Risk |
|---|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Remove `draft_ready` from `DecisionCodes(CaseCreated)` and `ParseDecision`; `case_created` becomes the sole persisted code for `IntakeDecision.CaseCreated`. | Receipt reads and filters must continue to handle every current decision and fail visibly for unknown codes. |
| `src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs` | Remove the `draft_ready` Operations mapping branch. | Current `case_created` processing must remain reported as succeeded. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Remove the obsolete legacy-compatibility explanation. Keep the rule that a processing decision does not prove Case existence. | Wording must not restore a manual acceptance gate or make the decision code Case authority. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | Remove the obsolete `draft_ready` comment; do not change acceptance policy. | Avoid unrelated eligibility changes. |
| Eleven integration-test fixtures listed below | Replace or remove incidental `draft_ready` seed values. Use `case_created` only where the test actually needs a definitive processed receipt; omit unnecessary receipt/Case setup where the test does not need it. | Keep each unrelated test’s real purpose intact. No fixture receives special “historical compatibility” protection. |
| `docs/design.md` | Remove the statement that `draft_ready` remains read-compatible. State only the current `case_created` processing decision and Case-link authority. | Do not conflate processing completion with Case creation. |
| `docs/current-architecture.md` | Remove the as-built compatibility claim and describe `case_created` as the sole current persisted decision code. | Must match the implemented source after removal. |
| `docs/temp-plans/simpli-010.md` | Create the required root task plan covering this bounded removal and verification. | Must remain a transient implementation plan, not a requirements source. |

## Test fixtures to clean

Every current occurrence is removable; none represents Case data the product must preserve:

- `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs`
- `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ConcurrencyTokenPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/EvaHandoffPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderInspectionModeAcceptanceTests.cs`
- `tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs`
- `tests/Pegasus.IntegrationTests/VehicleWorkflowTerminalTests.cs`

For the two tests that exercise unrelated historical migrations, replace `draft_ready` with the minimum neutral/current fixture value needed by those tests. Git history—not a live test literal—preserves the old implementation.

## Ripple effects

- Receipt detail, queue filters/counts, retained-mail projection, Case acceptance, Operations, and MCP all consume the central persisted decision mapping. Focused verification must prove current `case_created` behavior and unknown-code failure remain intact.
- `CaseCreated` filtering changes from two persisted strings to one. Existing receipt persistence/list tests must prove only `case_created` is selected.
- `ProcessQueuedIntake`, `AllocateIntake`, Case links, allocation attempts, retry taxonomy, and Case/reference creation require no change. If implementation appears to require them, stop: that is scope expansion or overlap with SIMPLI-009.
- A final repository search must show no `draft_ready` occurrence in application source, canonical current-state/design documentation, or active test fixtures. Kanmer research and git history may still mention it as task evidence.
- SIMPLI-009 intentionally overlaps the processing area. Recheck its exact diff immediately before implementation and do not edit the same source hunk concurrently.

## Out of scope

- No EF migration or migration designer.
- No production/Azure SQL query, data inspection, repair script, deployment, or live readback.
- No preservation of predecessor or pre-release Cases, receipts, or application state.
- No change to `IntakeDecisionPolicy.CanBecomeCase`, automatic allocation behavior, Case/reference identity, Case links, retries, or recovery.
- No new compatibility abstraction, feature flag, state, service, project, store, runtime, or deployment unit.
- No edits to operator notes, PRD, FRD, ADRs, capabilities, operations, or runbook; product behavior and live-state claims do not change.
