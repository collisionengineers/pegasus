# Files — DELIV-058

## Where the change lands

| Path | Why |
|---|---|
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Assert that `ProcessIntake` takes the existing `InstructionExtractionPolicySelector`, does **not** directly take `IInstructionExtractionPolicy`, and that the selector's sole constructor consumes `IEnumerable<IInstructionExtractionPolicy>`. Retain the existing Core-only/non-Infrastructure policy implementation assertions. |
| `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Extend the existing exact constructor parameter array with `IImageIntakeCasePairing` and `ITriageCasePairing` in the positions used by the current Worker function. Add only the existing Triage namespace import needed by that assertion. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | The authoritative constructor has `InstructionExtractionPolicySelector`, uses it for the instruction role, and owns no direct policy parameter. Read-only evidence; do not change it. |
| `src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs` | The selector's sole constructor takes `IEnumerable<IInstructionExtractionPolicy>`; it is the Core collection/ambiguity boundary that must remain explicit. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | The timer function has both pairing dependencies and calls both bounded reconciliation methods in sequence; the expected-array order must exactly match it. Read-only evidence; do not change it. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Existing composition supplies every policy and the selector, but this ticket makes no composition change. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Replays and scheduled reconciliation must resume image and Triage pairing; this gives the Worker dependencies product meaning. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Core owns policy selection independent of transport; no runtime extraction rule or route changes belong here. |
| `docs/engineering.md` | Governs truthful failures, meaningful assertions, scope discipline and the sole heavy-verifier rule. |
| `tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj` | The existing focused/full architecture test project is the only validation target after the verifier slot is granted. |

## Ripple effects

The two assertions fail against the current runtime shapes, so their corrections allow the existing architecture suite to continue detecting: (a) an accidental direct policy dependency or a selector that stops owning its policy collection, and (b) omission/reordering of either Worker pairing reconciliation. No production caller, dependency registration, build asset, API, documentation or generated artifact changes.

## Out of scope

- Any file under `src/`, including `ProcessIntake.cs`, `InstructionExtractionPolicySelector.cs`, `DependencyInjection.cs`, and `IntakeFunctions.cs`.
- `tests/Pegasus.ArchitectureTests` files other than the two listed above, including D56-owned verifier work.
- [[INTK-002]] adapter-fault naming and Web composition work.
- Every principal inventory/source, PR #706 feature change, infrastructure, cloud operation, release action, schema/data, compatibility path or new dependency.
- Test/build execution until the active D56 sole verifier grants the slot.
