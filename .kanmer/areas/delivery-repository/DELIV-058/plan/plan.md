# Plan — DELIV-058: Align architecture assertions with current extraction selection and pairing composition

## Objective

Correct two stale architecture assertions so they protect the current Core selector collection boundary and exact Worker pairing-reconciliation composition, without changing runtime behavior.

## Starting state

Evidence: `research/research.md`@`f233a2f61bf771c3`; `files/files.md`@`2233f18eb23bedf3`. PR #706 run 34240260482 / job 102108502034 reported 2 architecture failures of 116. Research was read from a shared checkout and is not an execution base. An eventual execution packet must record a fresh resolved `origin/dev` SHA and a clean DELIV-058 worktree.

## Governing docs

- **Meets `docs/engineering.md`:** preserves meaningful, negative architectural assertions and reserves restore/build/test work for the sole verifier.
- **Meets `docs/frd/frd-02-intake-and-source-identity.md`:** retains the exact Worker contract for scheduled replay/reconciliation of unfinished image and Triage pairings.
- **Meets `docs/frd/frd-09-provider-and-intermediary-routes.md`:** retains Core-owned policy selection independent of transport; no route, policy or allocation behavior changes.

No governing-document change or ADR is needed.

## Required changes

1. In `DependencyDirectionTests`, assert `ProcessIntake` has `InstructionExtractionPolicySelector`, explicitly does **not** directly accept `IInstructionExtractionPolicy`, and assert the selector has one constructor receiving `IEnumerable<IInstructionExtractionPolicy>`. Retain independent Core-only/no-Infrastructure implementation assertions.
2. In `StagedArtifactReconciliationFunctionTests`, retain the exact ordered Worker constructor parameter array and add existing `IImageIntakeCasePairing` and `ITriageCasePairing` in their current positions. Add only the existing Core Triage namespace import if necessary.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Assert the selector, explicit absence of a direct policy parameter, and the selector's collection constructor while preserving policy ownership checks. |
| Modify | `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Preserve exact ordered equality and add both existing pairing dependencies. |

## Do not modify

- `src/**`
- `tests/Pegasus.ArchitectureTests/**` except the two Expected files
- `docs/**`, `infra/**`, `.github/**`, `scripts/**`, `corpus/**`, `reference/**`
- `.kanmer/**` except this ticket's Kanmer documents
- any D56 verifier state or [[INTK-002]] scope

## Constraints

No runtime, constructor, policy, DI, Worker function, Web composition, package, schema, release, cloud or dependency change. Do not reintroduce a compatibility assertion for a direct policy parameter. Do not weaken exact Worker equality to membership/subset matching. Do not add a helper/framework. No test/build/restore may run until D56 grants the sole verifier slot. Stop for root plan review: no ticket take, worktree/branch, implementation, commit, PR, or stage change.

## Ordered steps

### Step 1 — Prove the one Core selector boundary
- Preconditions: root approves this plan; an execution packet records a fresh `origin/dev` SHA and isolated DELIV-058 worktree.
- Files: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.
- Symbols: `IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary`, `ProcessIntake`, `InstructionExtractionPolicySelector`, `IInstructionExtractionPolicy`.
- Change: assert selector presence, direct policy absence, and the selector's single `IEnumerable<IInstructionExtractionPolicy>` constructor; retain implementation ownership assertions.
- Preserved behaviour: policies remain Core-owned; Infrastructure supplies no implementation; the test is still a direct constructor-boundary guard.
- Forbidden: runtime/DI/policy edits, helper abstraction, deleting ownership checks, or preserving both legacy and current parameter claims.
- Negative cases: a direct interface or concrete policy on `ProcessIntake`, or a selector without the collection constructor, must fail the assertion.
- Tests: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.
- Commands: none; Step 3 owns verification.
- Expected output: assertion-only change that guards the positive selector boundary and negative direct-policy boundary.
- Done when: only the planned assertion strengthens/updates are present.
- Deviation stop: current source does not match research, another file is needed, or authorization is absent.

### Step 2 — Complete the exact Worker pairing contract
- Preconditions: Step 1 is frozen; current Worker constructor still has both pairing parameters in the researched order.
- Files: `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`.
- Symbols: `FunctionDependsOnTheCanonicalStagedArtifactReconciler`, `StagedArtifactReconciliationFunction`, `IImageIntakeCasePairing`, `ITriageCasePairing`.
- Change: include both pairing interfaces at their actual positions in the exact expected array; add only a required existing namespace import.
- Preserved behaviour: complete ordered signature equality and canonical reconciliation composition.
- Forbidden: Worker change, loose membership assertion, reconciliation-limit/caller change, or INTK-002 expansion.
- Negative cases: omission, reorder or substitution of either pairing dependency must fail.
- Tests: `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`.
- Commands: none; Step 3 owns verification.
- Expected output: expected array exactly mirrors the current Worker signature.
- Done when: no third test/source file differs.
- Deviation stop: runtime code or another test becomes necessary.

### Step 3 — Verify under the D56 sole-verifier slot
- Preconditions: root grants the D56 slot after reviewing the frozen two-file diff and provides the fresh packet base. D56's current failures require its full TRX diagnosis; do not modify D56 before a separate authorization.
- Files: no additional modifications.
- Symbols: the two named architecture tests and `Pegasus.ArchitectureTests` project.
- Change: none.
- Preserved behaviour: no assertion is deleted/weakened; every exit, including failure, is recorded.
- Forbidden: concurrent heavy checks, skipped failure, source edit during verification, or Debug configuration.
- Negative cases: either focused failure or any full-project failure remains a failure; no later pass erases it.
- Tests: both focused tests then full existing architecture project.
- Commands: `dotnet restore ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode --configuration Release`; `dotnet build ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore`; `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary|FullyQualifiedName~FunctionDependsOnTheCanonicalStagedArtifactReconciler"`; `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- Expected output: both focused tests pass; full architecture project reports the existing 116-test result with exit 0.
- Done when: D56 records exact Release command exits against reviewed head.
- Deviation stop: unavailable slot, failed/inconclusive command, changed D56 diagnosis, or scope conflict.

## Acceptance checks

- The test proves positive selector presence, negative direct-policy absence, and selector ownership of its policy collection.
- The Worker test proves the entire ordered signature, including image and Triage pairing reconciliation.
- Only the two Expected test files change; no runtime caller/registration changes.
- Sole verifier runs only the stated Release commands and records outputs/exits.

## Commands

None are authorized during planning or implementation. After the D56 grant only, run Step 3's four Release commands in order from the fresh packet worktree.

## Failure and deviation rules

Stop/report a stale base, changed constructor shape, unexpected third file, assertion weakening risk, concurrent verifier activity, failed/inconclusive command, or D56 diagnosis conflict. Do not compensate with runtime edits, loose assertions, adapters, compatibility code or scope expansion. [[INTK-002]] remains separate.

## Stop condition

Stop in Preparing for root plan review. Do not take a branch/worktree, implement, execute checks, commit, create a PR, self-review, merge or move to Implementing without a new explicit root grant.
