# Checklist — DELIV-058

- [ ] Step 1 — In `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`, assert `InstructionExtractionPolicySelector` on `ProcessIntake`, assert direct `IInstructionExtractionPolicy` absence, and assert the selector's single `IEnumerable<IInstructionExtractionPolicy>` constructor while retaining Core-only ownership checks.
- [ ] Step 2 — In `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`, retain exact ordered equality and add existing image and Triage pairing dependencies in the Worker constructor's current positions.
- [ ] Step 3 — After root grants the sole D56 verifier slot and records a fresh `origin/dev` packet base, run only the Release locked restore/build, both focused architecture tests, and the existing full architecture project with truthful exits.
- [ ] [pre-review] Confirm the diff changes only the two planned architecture-test files, neither alters runtime composition nor weakens any positive/negative architectural assertion.
- [ ] [pre-review] Stop for D56 TRX diagnosis and root authorization; do not take the ticket, create a worktree/branch, implement, use Debug commands, merge or start another ticket.

## Progress notes

2026-09-08: Root plan review strengthened the selector assertion to cover both positive selector presence and negative direct-policy absence, plus the selector's collection constructor. All eventual verifier commands use `--configuration Release`. D56 has genuine unrelated failures pending full TRX diagnosis; no D56 edit is authorized. No branch, worktree, implementation, build, test, cloud operation or D56 verifier activity occurred.
