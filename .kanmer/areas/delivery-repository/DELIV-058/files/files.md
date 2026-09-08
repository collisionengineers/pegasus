# Files — DELIV-058

## Where the change lands

| Path | Why |
|---|---|
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Assert that `ProcessIntake` takes `InstructionExtractionPolicySelector`, has no parameter assignable to `IInstructionExtractionPolicy`, and that the selector's sole constructor consumes `IEnumerable<IInstructionExtractionPolicy>`. Retain existing Core-only/non-Infrastructure policy implementation assertions. |
| `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Extend the existing exact constructor parameter array with `IImageIntakeCasePairing` and `ITriageCasePairing` in the current Worker positions. Add only the existing Triage namespace import needed by that assertion. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | The current constructor has `InstructionExtractionPolicySelector`, uses it for the instruction role, and owns no direct/concrete extraction-policy parameter. Read-only evidence. |
| `src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs` | The selector's sole constructor takes `IEnumerable<IInstructionExtractionPolicy>`; it is the Core collection/ambiguity boundary that must remain explicit. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | The timer function has both pairing dependencies and calls each bounded reconciliation method; the expected-array order must exactly match it. Read-only evidence. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Existing composition supplies policies and selector; this ticket makes no composition change. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Replays/scheduled reconciliation resume image and Triage pairing, giving both Worker dependencies product meaning. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Core owns policy selection independent of transport; no route/policy/allocation behavior changes belong here. |
| `docs/engineering.md` | Governs truthful failures, meaningful assertions, scope discipline and shared heavy-verifier coordination. |

## Ripple effects

The corrections continue detecting (a) any direct interface or concrete policy parameter reintroduced on `ProcessIntake`, (b) a selector that no longer owns its policy collection, and (c) omission/reordering of either Worker pairing reconciliation. No caller, registration, build asset, API, documentation or generated artifact changes.

## Out of scope

- Any `src/**` file, including ProcessIntake, selector, DI and Worker function.
- Every architecture-test file except the two listed above.
- [[INTK-002]] adapter-fault naming and Web composition.
- D56 source/diagnosis/remediation, which is independent; only the shared verifier slot constrains test execution.
- Principal inventory/PR #706 feature work, infrastructure, cloud/release, schema/data, compatibility and new dependencies.
- Test/build execution until the active sole-verifier slot is granted.
