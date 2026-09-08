# Files — INTK-064

Evidence refreshed at dev a022fc4b2db87d4d2eeb14437b41f6d8344d63e6.
Research 1233ce70e848f312; supersedes map e960169a2d34216e.

## Production changes

| Path | Responsibility |
| --- | --- |
| src/Pegasus.Core/Triage/TriageContracts.cs | Narrow ITriageCasePairing caller port and automatic candidate/transaction contract on ITriageStore; distinct from manual TriageCaseLinkRequest. |
| src/Pegasus.Core/Triage/TriageLifecycle.cs | One ITriageCasePairing/TriageCasePairing command and post-create/replay call from existing CreateTriageFromIntake; preserve manual authorization/replay. |
| src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs | Eligible pending reads and existing serializable link/history transaction's SystemWorker-only path; current principal, origin, candidate, lease and manual-intent guards. |
| src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs | Context-bound form of both existing EfCaseMatchIndex query methods; no duplicate grammar/eliminator. |
| src/Pegasus.Core/Intake/AcceptIntake.cs | Formal acceptance and duplicate-replay pairing, preserving current INTK-063 image recovery. |
| src/Pegasus.Worker/IntakeFunctions.cs | Call pairing from the existing reconciliation timer and surface bounded failure counts/type. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | One automatic-command registration beside existing Triage commands; no ITriageLifecycle exists. |
| docs/frd/frd-03-triage.md | Automatic/manual linkage and distinct-reference Case terminology without normal Case/PO allocation or finding promotion. |
| docs/frd/frd-02-intake-and-source-identity.md | Triage association currentness/reversal/recovery; preserve accepted INTK-063 image rules. |
| docs/current-architecture.md | Actual automatic Triage callers/recovery after implementation, not a deployment claim. |

## Existing tests

| Path | Responsibility |
| --- | --- |
| tests/Pegasus.Core.Tests/Triage/TriageReplayTests.cs | Automatic authorization/replay contract and existing fake updates; preserve manual behavior. |
| tests/Pegasus.Core.Tests/Triage/AddTriageNoteTests.cs | Fixture-only ITriageStore new-member implementation; note assertions unchanged. |
| tests/Pegasus.IntegrationTests/QdosTriageCaseAssociationIntegrationTests.cs | Current formal/Triage arrival, identity/principal/lease/race/manual guards; replace obsolete no-auto-link expectation using the same genuine source. |
| tests/Pegasus.IntegrationTests/TriageFromIntakeIntegrationTests.cs | Actual Triage creation caller and inverse arrival order/replay. |
| tests/Pegasus.IntegrationTests/QdosTriageReplayIntegrationTests.cs | Durable retry and unchanged permanent reference/finding/completion behavior. |
| tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs | Actual SystemWorker pairing and replay under existing restricted connection factory; no copied permission list. |

The following direct AcceptIntake constructor fixtures need only the new
required pairing dependency supplied through an existing test-double style;
preserve every assertion and do not create an optional/no-op production path:

- tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs
- tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs
- tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs
- tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs
- tests/Pegasus.IntegrationTests/ProviderApiCaseDataSnapshotPersistenceTests.cs
- tests/Pegasus.IntegrationTests/ProviderInspectionModeAcceptanceTests.cs

## Context files

| Path | Constraint |
| --- | --- |
| src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs | Existing ExecuteDeclaredAsync and complete eliminator/replacement traversal; read only, no contract change needed. |
| src/Pegasus.Core/Intake/DurableIntake.cs | ProcessQueuedIntake already calls ICreateTriageFromIntake; that command can cover creation/replay without editing this caller. |
| src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs | Manual Staff/lease boundary and workflow completion helpers; do not relax it for automation. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs | Existing retained evidence and typed draft mapping; no new extraction/provenance owner. |
| src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs | Current immediate/replay/timer and visible-failure convention; image eligibility is not a Triage rule. |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs | Worker Triage/history baseline; full Triage detail reads would unnecessarily require findings access. |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260814092852_AddWorkerCaseCreationGrants.cs | Existing CaseHistory/Principals/CaseMatchIndex rights already cover needed automatic-link inputs. |

## Exclusions and ownership

No Web/MCP manual authorization change, new parser, queue, runtime, schema,
migration, broad grant, provider mutation, corpus change or generated UI asset.
No fake ITriageLifecycle wrapper, IServiceProvider lookup or manual construction
in Worker to bypass composition ownership. Do not read TriageFindings merely
to link: narrow candidate inputs suffice and preserve current Worker grants.

TICK-035 is Done/released. INTK-063 still owns its integrated-but-not-closed
acceptance/Worker/FRD/test surfaces, and TICK-085 owns Infrastructure DI.
Execution remains serialized until root confirms exact releases. Historical
INTK-060 and other linked ticket claims remain untouched.
