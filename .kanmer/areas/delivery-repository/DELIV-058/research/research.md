# Research — DELIV-058: architecture assertion reconciliation

## Question

Why do the two architecture tests in PR #706's current-head CI fail, and which assertion-only corrections preserve the current Core ownership and Worker reconciliation contracts without absorbing separate work?

## Findings

- `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` currently asserts that `ProcessIntake` directly receives `IInstructionExtractionPolicy`. Current `src/Pegasus.Core/Intake/ProcessIntake.cs` instead receives `InstructionExtractionPolicySelector` and uses `Select(..., InstructionDocumentSignature.InstructionRole)` before route/principal reconciliation; it only obtains a selected policy through that selector.
  - `src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs` is the Core owner of the collection boundary: it receives `IEnumerable<IInstructionExtractionPolicy>`, evaluates every signed profile against current instruction content, returns no policy on no-match, and reports all matches rather than letting registration order choose one.
  - `src/Pegasus.Infrastructure/DependencyInjection.cs` registers the existing policy collection and the selector, while every policy implementation remains in `Pegasus.Core.Intake`. The meaningful assertion is therefore the explicit selector constructor boundary, not the superseded direct single-policy parameter.
- `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` asserts an exact constructor parameter sequence but omits two actual parameters of `StagedArtifactReconciliationFunction`: `IImageIntakeCasePairing` and `ITriageCasePairing`.
  - `src/Pegasus.Worker/IntakeFunctions.cs` invokes each dependency's bounded `ReconcileAsync(50, cancellationToken)` from the existing timer function and logs its candidate/result counts. The test must retain the exact ordered array, adding these two dependencies in their real positions rather than weakening the assertion.
- `docs/frd/frd-02-intake-and-source-identity.md` requires image pairing to resume on both arrival orders and replays, with scheduled reconciliation retrying unfinished links; it also requires Triage creation, formal acceptance and replay to attempt the same principal-scoped match and scheduled reconciliation to retry unfinished links. Those requirements explain why both pairings are Worker reconciliation dependencies.
- `docs/frd/frd-09-provider-and-intermediary-routes.md` states that transport does not change extraction or automatic allocation, and that Core policies own the supported routes. This supports retaining a Core-owned, collection-aware extraction selection boundary.
- The provided CI evidence is run 34240260482 / unit job 102108502034: 2 architecture failures of 116 after the Core unit lane passed. `git log` identifies `0f1355108` as the current one-policy-set consolidation; PR #706 did not alter either runtime target or either architecture test.
- Kanmer duplicate search found no existing active owner for this two-test assertion repair. [[INTK-002]] remains a distinct backlog chore for adapter-fault naming and the Web composition boundary; it is not a dependency or write target. [[INTK-065]] is a linked, separate principal-evidence-inventory review ticket.
- No project-declared research sources apply to this ticket. No tests, builds, cloud reads/writes, branch take, worktree creation, or source changes occurred during this research.

## Implications

Amend exactly two existing architecture tests:
1. assert `InstructionExtractionPolicySelector` on `ProcessIntake`, retain the independent assertions that every extraction-policy implementation is Core-owned and Infrastructure supplies none;
2. retain the Worker constructor's exact ordered dependency assertion and add the two pairing interfaces at their actual constructor positions.

No constructor, selector, policy, DI, Worker function, production caller, adapter-fault handling, Web composition, or governing document changes are justified. A future executor must first refresh to a clean `origin/dev` base in its own approved worktree, then await the sole verifier's authorization before running any restore/build/test command.

## Open questions

None. The supplied task resolves the correction boundary and verification ownership.
