# Plan — SIMPLI-010: Remove `draft_ready` compatibility and keep Case-link authority

Written from the corrected `research.md` and `impact.md`.

## Approach

Remove `draft_ready` directly from runtime code, canonical documentation, and all active test fixtures. Pegasus has no retained application data requiring compatibility, so this task adds no migration, production inspection, repair, deployment, or live verification. `case_created` remains the sole persisted code for `IntakeDecision.CaseCreated`. The processing decision continues to express allocation eligibility; only the Case intake link proves that a Case/reference exists.

## Steps

1. **Synchronize the task records**
   - Create `docs/temp-plans/simpli-010.md` with this scope, sequencing, stop rules, verification commands, and owned-file inventory.
   - Correct SIMPLI-010’s ticket body so it no longer requires production-data inspection:
     - retain the receipt-to-Case-link authority goal;
     - state that `draft_ready` is removed directly;
     - state explicitly that no migration or live-data work is required.
   - Create Kanmer `checklist.md` from these steps.
   - Keep the ticket In Progress.

2. **Recheck branch and overlap safety**
   - Fetch `origin/dev`.
   - Confirm `task/simpli-010` is clean and based on the expected `origin/dev`.
   - Inspect `task/simpli-009` for new commits or working-tree changes.
   - Continue only if SIMPLI-009 has not started editing the same persistence mapping or documentation hunks.
   - If overlap has appeared, coordinate the exact files before editing; do not rebase, force-push, or touch SIMPLI-009’s worktree.

3. **Capture the removal baseline**
   - Run:
     ```powershell
     rg -n "draft_ready|DraftReady" src tests docs/current-architecture.md docs/design.md
     ```
   - Record the expected baseline:
     - compatibility mapping/filter in `EfIntakeReceiptStore`;
     - Operations mapping;
     - two obsolete source comments;
     - canonical design/current-architecture statements;
     - fixture values in eleven integration-test files.
   - Confirm there is no executable migration containing `draft_ready`.
   - Confirm no current writer emits it: `ToCode(IntakeDecision.CaseCreated)` must already return `case_created`.

4. **Simplify persisted decision mapping**
   - In `EfIntakeReceiptStore`:
     - replace the multi-code `CaseCreated` filter with an exact comparison against `ToCode(requested)`;
     - delete `DecisionCodes`, because it exists only to expand `CaseCreated` into `case_created` plus `draft_ready`;
     - remove the `draft_ready => IntakeDecision.CaseCreated` branch from `ParseDecision`;
     - remove comments describing legacy read compatibility.
   - Preserve all current mappings unchanged:
     - `case_created`;
     - `needs_sorting`;
     - `blocked_intake`;
     - `unsupported`;
     - `ocr_required`;
     - `technical_failure`;
     - `image_intake_registered`.
   - Preserve existing unknown-code behavior: an unsupported persisted value must fail visibly rather than be silently reinterpreted.

5. **Remove the remaining runtime compatibility branches**
   - In `EfOperationsStore`, remove `draft_ready` from the set mapped to `EmailOperationState.Succeeded`.
   - In `EfCaseAcceptanceStore`, remove only the obsolete `draft_ready` compatibility comment.
   - Do not change `IntakeDecisionPolicy.CanBecomeCase`, acceptance guards, allocation commands, retries, or transactions.
   - In `IntakeContracts`, remove the obsolete legacy paragraph and retain a concise current rule:
     - `CaseCreated` is a processing/allocation-eligibility decision;
     - the allocation/link projection determines whether a Case exists.
   - Do not edit `ProcessQueuedIntake`, `AllocateIntake`, Case-link persistence, allocation attempts, or retry/recovery behavior.

6. **Clean every active test fixture**
   - Replace each incidental `draft_ready` database value with `case_created` in:
     - `AssessmentPersistenceIntegrationTests`;
     - `CaseDataCompletenessPersistenceTests`;
     - `CaseMatchIntegrationTests`;
     - `CaseTaskArchivePersistenceTests`;
     - `CaseWorkflowMigrationTests`;
     - `CaseWorkflowPersistenceTests`;
     - `ConcurrencyTokenPersistenceTests`;
     - `EvaHandoffPersistenceTests`;
     - `ProviderInspectionModeAcceptanceTests`;
     - `TypedCaseDataMigrationTests`;
     - `VehicleWorkflowTerminalTests`.
   - For the two tests exercising unrelated historical migrations, do not preserve the old literal. Use `case_created` as the minimal decision value needed to satisfy their unrelated fixture relationships.
   - Do not change Cases, references, expected workflow states, migration targets, or assertions unrelated to the decision string.
   - Do not add a compatibility migration test: there is no supported data transition to prove.

7. **Update canonical documentation**
   - In `docs/design.md`:
     - remove the statement that `draft_ready` remains read-compatible;
     - retain `CaseCreated` as the processing decision;
     - retain the separate allocation states and Case-link authority;
     - retain the rule that there is no manual “accept before allocation” intake state.
   - In `docs/current-architecture.md`:
     - describe `case_created` as the sole persisted code for the current processing outcome;
     - state that it is not Case-existence authority;
     - retain the current allocation attempt and actual Case-link projection.
   - Do not edit operator notes, PRD, FRD, ADRs, capabilities, operations, or runbook because business behavior, deployment state, and runtime topology do not change.
   - Leave Kanmer research/history and git history free to mention `draft_ready` as evidence of the removed implementation.

8. **Perform a bounded diff review before tests**
   - Run `git diff --check`.
   - Review `git diff --stat` and `git diff --name-only`.
   - Confirm:
     - no migration or model snapshot changed;
     - no application project boundary changed;
     - no allocation/replay code changed;
     - no files owned by SIMPLI-009 changed;
     - every test edit is limited to the fixture decision value;
     - documentation describes current behavior rather than a future migration.
   - Run:
     ```powershell
     rg -n "draft_ready|DraftReady" src tests docs/current-architecture.md docs/design.md
     ```
   - Expected result: no matches.

9. **Run focused verification**
   - Restore and build:
     ```powershell
     dotnet restore ./Pegasus.slnx --locked-mode
     dotnet build ./Pegasus.slnx --configuration Release --no-restore
     ```
   - Run focused Core and persistence/caller coverage:
     ```powershell
     dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build

     dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakeStablePersistenceTests|FullyQualifiedName~QdosIntakeWebTests|FullyQualifiedName~OperationsPersistenceTests|FullyQualifiedName~CaseWorkflowMigrationTests|FullyQualifiedName~TypedCaseDataMigrationTests"
     ```
   - Verify:
     - new receipts persist `case_created`;
     - `CaseCreated` filtering returns current `case_created` receipts;
     - unknown persisted decision codes still fail visibly;
     - Operations maps `case_created` to succeeded;
     - MCP continues to separate processing decision from allocation status;
     - unrelated historical migration tests retain their actual behavior.

10. **Run repository-level verification**
    - Run:
      ```powershell
      dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build

      dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
      ```
    - If a suite times out or is interrupted, record it as unverified—not passed—and rerun the smallest affected lane needed for a definitive result.
    - Repeat:
      ```powershell
      git diff --check
      rg -n "draft_ready|DraftReady" src tests docs/current-architecture.md docs/design.md
      ```
    - Confirm the worktree contains only planned files.

11. **Record proof and complete the checklist**
    - Tick checklist items as each step completes.
    - Write Kanmer `proof.md` with:
      - exact commit/head tested;
      - exact commands;
      - exit codes and test counts;
      - zero-match removal search;
      - diff scope;
      - confirmation that no migration, production query, deployment, Case mutation, or compatibility layer was introduced;
      - any unverified lane or environmental limitation.
    - Append progress notes rather than rewriting checklist history.

12. **Commit and prepare the PR**
    - Commit only SIMPLI-010-owned changes in small logical slices:
      1. runtime compatibility removal and fixture cleanup;
      2. canonical documentation and task plan/proof updates.
    - Push `task/simpli-010`.
    - Open a PR targeting `dev`.
    - State in the PR:
      - direct removal of unreleased `draft_ready` compatibility;
      - `case_created` is now the sole current persisted code;
      - Case-link authority and allocation behavior are unchanged;
      - no migration or live-data operation exists;
      - exact verification performed.

13. **Independent review and completion**
    - Have an agent that did not implement the change review:
      - whether the plan missed any ticket requirement;
      - whether implementation missed any plan step;
      - whether any decision code still competes with the Case link;
      - whether removal widened into allocation/recovery changes.
    - Address only correctness or scope findings.
    - Require green CI and passing independent review before merging to `dev`.
    - Do not merge `dev` to `main`; that requires separate `MERGE AUTH GRANTED`.
    - After the task PR merges:
      - perform only workflow-authorized cleanup;
      - remove the task-owned temporary plan in a maintenance push if appropriate;
      - release the Kanmer claim;
      - remove the merged worktree and branch;
      - move SIMPLI-010 to Done only after `proof.md` exists and cleanup is complete.

## Verification

Proof must include:

- zero active `draft_ready`/`DraftReady` references in source, active tests, design, and current architecture;
- locked restore and Release build;
- focused Core, persistence, Operations, MCP, filter, and historical-migration tests;
- architecture tests and the canonical non-corpus solution test;
- exact-head and bounded-diff review;
- confirmation that no migration, live query, deployment, Case mutation, or allocation/recovery change occurred.

## Acceptance criteria

- No `draft_ready`/`DraftReady` occurrence remains in application source, active tests, `docs/design.md`, or `docs/current-architecture.md`.
- No migration, data repair, production inspection, cloud write, or Case mutation is introduced.
- All eleven affected fixture files continue testing their original behavior with current decision data.
- Current `case_created` persistence, filtering, Operations reporting, MCP projection, and unknown-code failure remain green.
- `ProcessQueuedIntake`, allocation policy, Case links, references, retries, and recovery are unchanged.
- Locked restore, Release build, focused tests, architecture tests, and canonical non-corpus tests pass or any environmental limitation is explicitly recorded as unverified.
- Independent review passes and CI is green before merge.

## Risks / stop rules

- Stop if SIMPLI-009 begins modifying the same persistence or documentation hunk.
- Stop if removal appears to require a migration, production-data query, Case mutation, or allocation/replay change; those contradict the corrected research and approved scope.
- Stop if a test relies on `draft_ready` for behavior rather than incidental setup; re-check the authoritative current requirement before changing that test.
- Do not preserve pre-release compatibility merely because git history or an unrelated migration test once used the old literal.
