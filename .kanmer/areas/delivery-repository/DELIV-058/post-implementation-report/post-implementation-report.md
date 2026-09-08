# Post-implementation report — DELIV-058

## Result

Corrected the two stale architecture assertions identified by PR #706 CI without changing runtime code, composition, policy, or dependencies.

## Changed files

- `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`
  - Requires `ProcessIntake` to take `InstructionExtractionPolicySelector`.
  - Prohibits every constructor parameter assignable to `IInstructionExtractionPolicy`, covering both the direct interface and concrete policies.
  - Requires the selector's sole constructor to receive `IEnumerable<IInstructionExtractionPolicy>`.
  - Retains the Core-owned/no-Infrastructure policy-implementation assertions.
- `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`
  - Retains exact ordered constructor equality and adds `IImageIntakeCasePairing` and `ITriageCasePairing` in the Worker’s current positions.

## Contract and scope

The changes preserve the explicit Core policy-selection boundary required by FRD-09 and the scheduled image/Triage pairing-reconciliation composition required by FRD-02. No `src/**`, Web, DI, schema, dependency, release, D56, or INTK-002 file changed.

Base: `origin/dev` at `a1f0bfe260ea05df531df6e0ca3109141e7697da`.
Approved frozen blobs: `DependencyDirectionTests.cs` `77ca689ea1d91d694429cdcdee74ffd4ab29daf4`; `StagedArtifactReconciliationFunctionTests.cs` `80b74ef998d4ba905cd4b6dc05b10cea1945cd87`.
Implementation commit: `71c1bc1266583459d40b82c3d19d59af632afa7d`.

## Verification

The designated sole host verifier recorded PASS in `scratch/verify.md`:

- locked restore: exit 0;
- Release build with `--no-restore`: exit 0, 0 warnings and 0 errors;
- focused architecture tests: 2 passed, 0 failed;
- full architecture project: 116 passed, 0 failed.

The verifier’s postcheck retained the same two-file diff, clean `git diff --check`, and the approved blobs. No broader candidate or application-suite PASS is claimed.

## Review handoff

PR [#708](https://github.com/collisionengineers/pegasus/pull/708) targets `dev`, has head `71c1bc1266583459d40b82c3d19d59af632afa7d`, and is ready for independent review. The ticket is in Review. The reviewer must confirm the two-file scope, selector/collection negative boundary, and exact Worker constructor ordering. Exact-merge verification remains required after review.
