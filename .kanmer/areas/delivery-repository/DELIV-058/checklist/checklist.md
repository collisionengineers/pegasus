# Checklist — DELIV-058

- [ ] Step 1 — From a ready packet, refresh `origin/dev`, create only `.worktrees/deliv-058` on `DELIV-058-architecture-assertions`, and record the isolated workspace/branch through Kanmer take.
- [ ] Step 2 — In `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`, assert selector presence, absence of any ProcessIntake parameter assignable to `IInstructionExtractionPolicy`, and the selector's sole `IEnumerable<IInstructionExtractionPolicy>` constructor while retaining Core-only ownership checks.
- [ ] Step 3 — In `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`, retain exact ordered equality and add existing image/Triage pairing dependencies in their Worker positions.
- [ ] Step 4 — Run only static name/diff/hash checks, freeze the two-file diff for root/static review, and defer .NET verification to the shared slot.
- [ ] [pre-review] Confirm neither D56 source nor [[INTK-002]] scope is touched; D58 is independent except for the shared verifier slot.
- [ ] [pre-review] Do not run restore/build/test, commit, push, open a PR, merge or start another ticket before the next explicit grant.

## Progress notes

2026-09-08: Root approved the bounded two-test target. Plan correction: restore uses only `--locked-mode`; later build/tests use `--configuration Release`. The Core negative claim covers every ProcessIntake parameter assignable to `IInstructionExtractionPolicy`, not just the direct interface. D56 diagnosis/remediation is separate; only the verifier slot is shared.
