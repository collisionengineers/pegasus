# Plan — DELIV-058: Align architecture assertions with current extraction selection and pairing composition

## Objective

Correct two stale architecture assertions so they protect the current Core selector collection boundary and exact Worker pairing-reconciliation composition, without runtime behavior change.

## Starting state

Evidence: `research/research.md`@`f233a2f61bf771c3`; `files/files.md`@`8cc22ccff13751f2`. PR #706 run 34240260482 / job 102108502034 reported 2 architecture failures of 116. Research was read from a shared checkout, not an execution base. Execution must start from a fresh resolved `origin/dev` SHA in a clean DELIV-058 worktree.

## Governing docs

- **Meets `docs/engineering.md`:** retains meaningful positive/negative architecture checks and uses the shared heavy-verifier slot truthfully.
- **Meets `docs/frd/frd-02-intake-and-source-identity.md`:** retains exact Worker reconciliation composition for image and Triage replays.
- **Meets `docs/frd/frd-09-provider-and-intermediary-routes.md`:** retains Core policy selection independently of transport without altering routes, policies or allocation.

No governing-document change or ADR is needed.

## Required changes

1. In `DependencyDirectionTests`, assert `ProcessIntake` includes `InstructionExtractionPolicySelector`; assert no constructor parameter is assignable to `IInstructionExtractionPolicy` (covering direct interface and every concrete policy); assert the selector has one constructor receiving `IEnumerable<IInstructionExtractionPolicy>`; retain Core-only/no-Infrastructure policy implementation checks.
2. In `StagedArtifactReconciliationFunctionTests`, preserve the exact ordered Worker constructor parameter equality and add existing `IImageIntakeCasePairing` and `ITriageCasePairing` at their current positions. Add only the existing Triage import if necessary.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Positive selector and selector-collection assertions; negative absence of every assignable policy parameter; retain ownership checks. |
| Modify | `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Exact ordered equality including both pairing dependencies. |

## Do not modify

- `src/**`
- `tests/Pegasus.ArchitectureTests/**` except the two Expected files
- `docs/**`, `infra/**`, `.github/**`, `scripts/**`, `corpus/**`, `reference/**`
- `.kanmer/**` except this ticket's Kanmer documents
- [[INTK-002]] or any D56 source/diagnosis/remediation

## Constraints

No runtime, constructor, policy, DI, Worker function, Web composition, package, schema, release, cloud or dependency change. Do not preserve a compatibility assertion for an obsolete direct policy parameter. Do not loosen the exact Worker array. No helper/framework. The shared host verifier slot is the only dependency on D56; D58 implementation is otherwise independent. No restore/build/test until that slot is granted. Before grant, refresh `origin/dev`, create only `.worktrees/deliv-058` / `DELIV-058-architecture-assertions`, then freeze the two-file diff for root/static review. No commit, push, PR or merge.

## Ordered steps

### Step 1 — Refresh the approved execution base and take the ticket
- Preconditions: root's explicit approval and a ready Kanmer packet.
- Files: no repository source file.
- Symbols: `origin/dev`, DELIV-058 ticket claim.
- Change: fetch `origin/dev`; create the isolated `.worktrees/deliv-058` worktree on `DELIV-058-architecture-assertions`; record the exact workspace/branch through Kanmer take.
- Preserved behaviour: no shared checkout, board worktree, D56 workspace or source file is changed.
- Forbidden: using a stale/local base, reusing another ticket workspace, taking D56, reset/clean, commit/push/PR.
- Negative cases: unavailable/divergent base, dirty new worktree, occupied workspace or claim conflict stops work.
- Tests: none.
- Commands: `git fetch origin dev`; `git worktree add .worktrees/deliv-058 -b DELIV-058-architecture-assertions origin/dev`.
- Expected output: fresh isolated ticket worktree at the resolved origin/dev base.
- Done when: Kanmer records the exact branch and worktree.
- Deviation stop: packet/claim/worktree validation refuses.

### Step 2 — Prove the one Core selector boundary
- Preconditions: Step 1 complete; source matches the researched constructor shapes.
- Files: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.
- Symbols: `IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary`, `ProcessIntake`, `InstructionExtractionPolicySelector`, `IInstructionExtractionPolicy`.
- Change: assert selector presence; assert no ProcessIntake constructor parameter has `IInstructionExtractionPolicy.IsAssignableFrom(parameterType)`; assert `InstructionExtractionPolicySelector` has a single constructor with `IEnumerable<IInstructionExtractionPolicy>`; retain ownership assertions.
- Preserved behaviour: Core owns implementations; Infrastructure supplies none; test guards both positive selector and negative direct/concrete policy dependency.
- Forbidden: source/DI/policy edits, helper extraction, ownership-check deletion, or legacy/current dual claim.
- Negative cases: direct interface, QDOS/PCH/other concrete policy, or selector collection-boundary regression fails.
- Tests: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.
- Commands: none; verification is deferred.
- Expected output: assertion-only change in the expected file.
- Done when: the changed assertion proves both intended directions.
- Deviation stop: source shape differs or another file becomes necessary.

### Step 3 — Complete the exact Worker pairing contract
- Preconditions: Step 2 frozen; Worker signature retains the researched dependency order.
- Files: `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`.
- Symbols: `FunctionDependsOnTheCanonicalStagedArtifactReconciler`, `StagedArtifactReconciliationFunction`, `IImageIntakeCasePairing`, `ITriageCasePairing`.
- Change: add both pairing interfaces at current positions in the exact expected array and only required existing namespace import.
- Preserved behaviour: complete ordered equality and canonical pairing composition.
- Forbidden: Worker change, loose membership check, reconciliation caller/limit change, INTK-002 or D56 scope.
- Negative cases: omission/reorder/substitution of either pairing dependency fails.
- Tests: `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`.
- Commands: none; verification is deferred.
- Expected output: array exactly mirrors Worker signature.
- Done when: only the two Expected files differ.
- Deviation stop: third test/source file required.

### Step 4 — Freeze for root/static review and later sole verification
- Preconditions: Steps 2–3 complete.
- Files: no additional modifications.
- Symbols: two changed test files and their architecture project.
- Change: record source diff/hash and stop for review.
- Preserved behaviour: no assertion deleted/weakened and no check output invented.
- Forbidden: test/build before slot, concurrent heavy verification, commit/push/PR/merge.
- Negative cases: undeclared file or unexpected diff is a stop, not cleanup.
- Tests: later: two focused tests then full architecture project, under the granted shared verifier slot.
- Commands: now only `git diff --check`, `git diff --name-only`, and `git hash-object` for changed files. Later only: `dotnet restore ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode`; `dotnet build ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore`; `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary|FullyQualifiedName~FunctionDependsOnTheCanonicalStagedArtifactReconciler"`; `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- Expected output: two-file frozen diff; later, truthful focused/full Release test exits.
- Done when: root/static review receives changed-file list and hashes.
- Deviation stop: any check failure/inconclusive outcome, reviewer expansion, or verifier-slot absence.

## Acceptance checks

- `ProcessIntake` asserts selector presence and absence of every assignable extraction policy parameter; selector's policy-collection constructor remains explicit.
- The Worker test proves the full ordered signature including image/Triage pairing.
- Only two Expected files change; no runtime caller/registration change.
- Future verification uses locked restore without configuration and Release-only build/test commands.

## Commands

Before review, only Step 1 Git setup and Step 4 static/hash commands are authorized. The four .NET commands are deferred to the granted shared verifier slot; restore is `--locked-mode` with no `--configuration`, while build and tests use Release.

## Failure and deviation rules

Stop/report stale base, source-shape drift, unexpected third file, assertion weakening, workspace conflict, failed/inconclusive command or review conflict. Do not compensate with source edits, loose checks, adapters, compatibility or scope expansion. [[INTK-002]] and D56 diagnosis/remediation remain separate.

## Stop condition

After the two-file diff and hashes are frozen, stop for root/static review and the shared verifier decision. Do not test, build, commit, push, open a PR, self-review, merge or modify D56.
