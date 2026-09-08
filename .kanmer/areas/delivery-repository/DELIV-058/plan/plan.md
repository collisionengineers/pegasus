# Plan — DELIV-058: Align architecture assertions with current extraction selection and pairing composition

## Objective

Correct the two stale architecture assertions so they protect the current Core extraction-selection boundary and the current Worker pairing-reconciliation composition without changing runtime behavior.

## Starting state

Evidence: `research/research.md`@`f233a2f61bf771c3`; `files/files.md`@`2c41ade4ba5efb6e`. Supplied CI evidence is PR #706 run 34240260482, unit job 102108502034: 2 of 116 architecture tests fail after the Core unit lane passed. Research was read from the shared checkout only; it is not an execution base. Before any implementation, the execution packet must record a fresh resolved `origin/dev` SHA and a clean ticket worktree.

## Governing docs

- **Meets `docs/engineering.md`:** preserves meaningful tests, does not weaken an assertion merely to obtain green, and reserves all restore/build/test work for the sole verifier.
- **Meets `docs/frd/frd-02-intake-and-source-identity.md`:** keeps the Worker contract exact for the scheduled replay/reconciliation of unfinished image and Triage pairings.
- **Meets `docs/frd/frd-09-provider-and-intermediary-routes.md`:** keeps the extraction-policy selection boundary in Core and does not alter route, policy or allocation behavior.

No governing document changes or ADR are needed.

## Required changes

1. In `DependencyDirectionTests`, replace only the stale assertion that `ProcessIntake` directly accepts `IInstructionExtractionPolicy` with an assertion for `InstructionExtractionPolicySelector`. Preserve the independent checks that the policy implementations are in Core and none is duplicated in Infrastructure.
2. In `StagedArtifactReconciliationFunctionTests`, retain the single exact ordered constructor-parameter assertion and add the existing `IImageIntakeCasePairing` and `ITriageCasePairing` interfaces in their current constructor positions. Add the existing Core Triage namespace import if required.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Assert the selector collection boundary rather than an obsolete direct policy parameter; preserve all policy-ownership assertions. |
| Modify | `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Preserve the exact parameter sequence and include both existing pairing dependencies. |

## Do not modify

- `src/**`
- `tests/Pegasus.ArchitectureTests/**` except the two Expected files
- `docs/**`
- `infra/**`, `.github/**`, `scripts/**`, `corpus/**`, `reference/**`
- `.kanmer/**` except the ticket documents already recorded by Kanmer
- any D56 verifier state or [[INTK-002]] scope

## Constraints

No runtime, constructor, policy, DI, Worker function, Web composition, package, schema, release, cloud or dependency change. Do not add a compatibility assertion for the former direct-policy constructor. Do not replace exact constructor-array equality with a looser contains/subset assertion. No test/build/restore command may run until the D56 sole verifier actively grants its slot. Stop at planning review; do not take the ticket, create a worktree/branch, implement, commit, open a PR, or move beyond Preparing.

## Ordered steps

### Step 1 — Rebase the assertion on the Core selector boundary
- Preconditions: root approves this plan; an execution packet records a fresh `origin/dev` SHA and an isolated DELIV-058 worktree; the verifier slot is not used for editing.
- Files: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.
- Symbols: `IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary`, `ProcessIntake`, `InstructionExtractionPolicySelector`, `IInstructionExtractionPolicy`.
- Change: replace only the constructor expectation for a direct extraction-policy interface with the selector type; retain implementation ownership/non-duplication assertions.
- Preserved behaviour: every existing policy remains Core-owned; Infrastructure still contains no implementation; the test remains a constructor-boundary assertion.
- Forbidden: modifying `ProcessIntake`, selector behavior, policy registrations, or removing the ownership assertions.
- Negative cases: a direct policy constructor parameter, a concrete policy constructor parameter, or an Infrastructure policy implementation must fail the architectural claim.
- Tests: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.
- Commands: no command in this implementation step; verification is deferred to Step 3 under the sole verifier.
- Expected output: one assertion type correction, with the rest of the test's policy-ownership coverage intact.
- Done when: the file diff is limited to the current selector contract and remains within Expected files.
- Deviation stop: selector/runtime shape differs from researched evidence, any other file becomes necessary, or root/verifier authority is absent.

### Step 2 — Complete the exact Worker pairing dependency contract
- Preconditions: Step 1 is frozen and the Worker constructor still has both pairing parameters in the researched order.
- Files: `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`.
- Symbols: `FunctionDependsOnTheCanonicalStagedArtifactReconciler`, `StagedArtifactReconciliationFunction`, `IImageIntakeCasePairing`, `ITriageCasePairing`.
- Change: add the existing pairing interfaces to the exact expected parameter array at their current constructor positions; add only the required existing namespace import.
- Preserved behaviour: the test continues to assert the full ordered constructor signature and canonical reconciliation dependencies.
- Forbidden: changing the Worker function, replacing equality with loose membership, changing reconciliation limits/callers, or absorbing INTK-002.
- Negative cases: omission, reordering, or substitution of either pairing dependency must continue to fail.
- Tests: `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`.
- Commands: no command in this implementation step; verification is deferred to Step 3 under the sole verifier.
- Expected output: the expected array mirrors the current Worker signature exactly.
- Done when: only the two Expected files differ and a static diff review shows no source/runtime change.
- Deviation stop: any runtime file, third test file, or non-pairing responsibility is implicated.

### Step 3 — Verify under the sole host verifier
- Preconditions: root grants the D56 verifier slot after reviewing the frozen two-file diff and provides the fresh packet base.
- Files: no additional modifications.
- Symbols: the two named architecture tests and `Pegasus.ArchitectureTests` project.
- Change: none.
- Preserved behaviour: no assertion is deleted or weakened; every command exit is recorded, including failures.
- Forbidden: concurrent heavy checks, skipped failures, source edits while verifying, or a broader suite substitution.
- Negative cases: either focused assertion failure or any full-project failure remains a failure for disposition; it is not hidden by a later pass.
- Tests: the two focused architecture tests followed by the full existing architecture project.
- Commands: `dotnet restore ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode`; `dotnet build ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --no-restore`; `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --no-build --filter "FullyQualifiedName~IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary|FullyQualifiedName~FunctionDependsOnTheCanonicalStagedArtifactReconciler"`; `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --no-build`.
- Expected output: both focused tests pass and the full architecture project reports its existing 116-test result with exit 0.
- Done when: the verifier records exact command exits against the reviewed head.
- Deviation stop: the verifier slot is unavailable, any command fails/is inconclusive, or validation identifies a scope conflict.

## Acceptance checks

- `ProcessIntake` is asserted to depend on `InstructionExtractionPolicySelector`, and the test still establishes that actual extraction-policy implementations are Core-only.
- The Worker test asserts the full ordered signature, including both image and Triage reconciliation pairings.
- The implementation diff names only the two Expected files.
- No runtime production caller or registration changes.
- Sole verifier runs the exact focused and full architecture commands after authorization, records outputs/exits, and preserves failures.

## Commands

No project command is authorized during planning or implementation. Only after the sole verifier's grant, run the Step 3 commands in that exact order from the fresh packet worktree.

## Failure and deviation rules

Stop and report any stale packet base, changed runtime constructor, unexpected third file, inability to preserve exact assertion strength, concurrent verifier activity, or failed/inconclusive command. Do not compensate with source edits, loosening tests, adapters, compatibility code or scope expansion. [[INTK-002]] remains separate.

## Stop condition

Stop now in Preparing for root plan review. Do not take a branch/worktree, implement, execute checks, commit, create a PR, self-review, merge, or move the ticket to Implementing without a new explicit root grant.
