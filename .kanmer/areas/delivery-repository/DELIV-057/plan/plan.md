# Plan — DELIV-057: seed the historical vehicle lookup migration schema accurately

## Objective

Estimated diff: one modified source line in one test fixture. Restore the exact two required historical `Cases` columns in the vehicle-lookup backfill seed so its three existing tests reach their unchanged migration assertions.

## Starting state

`SeedCaseWithObservationAsync` in `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs` migrates to `20260822195419_CorrectIntakePhotographSemanticRole` but its `Cases` insert omits that schema's non-null `InstructionConfirmedByStaff` and `ImagesConfirmedByStaff` columns. The target and backfill migration designers both declare those columns; `20260907221500_RemoveCaseStaffConfirmation` removes them only later. Evidence: `research/research.md`@`6a70b524e9fbf46f`, `files/files.md`@`d57d7578f4b648f4`.

## Governing docs

- `docs/engineering.md` — Meets the focused-evidence rule by retaining existing assertions and requesting the existing class filter from sole host verifier DELIV-053. This plan changes no production policy, schema, or operating procedure.

## Required changes

Add both historical column names and the already-established `true` seed values to the existing `INSERT INTO Cases` statement in `SeedCaseWithObservationAsync`. Preserve the migration target, seed entities, observation data, backfill operations, all assertions, and test names exactly.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs` | Make the shared pre-migration `Cases` insert faithful to its historical target schema. |

## Do not modify

- `src/**` — no migration, model, snapshot, grant, or production-code change.
- `docs/**` — no documentation change.
- `tests/**` except `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs` — no assertion, test, or fixture expansion.
- `.github/**`, `scripts/**`, package/dependency inputs, generated artifacts, and `corpus/**`.

## Constraints

- The fixture models the schema at the explicit pre-backfill migration target, not the current production schema.
- Seed only the target schema's required columns using the established boolean values; do not fabricate additional domain data.
- Do not restore a current production column, add compatibility behavior, or change persistence permissions.
- The optional data-migration overlay is not selected: this ticket changes neither persistent data nor schema, only a disposable test seed.
- No local build, test, verification script, browser operation, capture, packaging, cloud action, commit, push, PR, merge, or independent-review action is authorized in this execution pass. Request all test evidence through the parent/sole verifier.
- The unrelated dirty documentation paths in the shared checkout are not this ticket's work and must remain untouched.

## Quality lenses

- Reuse — applied: amend the existing shared seed rather than create a parallel historical fixture.
- Simplification — applied: one seed statement change; no helper, abstraction, or compatibility path.
- Efficiency — skipped: this fixture setup is unchanged apart from required column values; no repeated or blocking work is introduced.
- Altitude — applied: no production policy or schema mechanism is added; the change stays at the test fixture boundary.

## Ordered steps

### Step 1 — Restore the required historical seed columns
- Preconditions: the target migration designer still shows both staff-confirmation columns on `Cases`; the ticket worktree is the recorded `.worktrees/deliv-057` branch.
- Files: `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs`.
- Symbols: `VehicleLookupBackfillTests.SeedCaseWithObservationAsync`.
- Change: add `InstructionConfirmedByStaff` and `ImagesConfirmedByStaff` with the existing `true` values to the `Cases` insert.
- Preserved behaviour: all three tests retain their migration target, existing fixture inputs, backfill/transition assertions, fact-precedence assertion, and idempotency assertion.
- Forbidden: any test assertion change; any production migration, model, snapshot, grant, dependency, compatibility, or new fixture-data change.
- Negative cases: do not seed only the first reported column, and do not remove the historical target merely to make the insert fit the current model.
- Tests: `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs`.
- Commands: parent-designated sole verifier, after a sequential affected Release build if it requires one, runs `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~VehicleLookupBackfillTests"`.
- Expected output: the selection executes the three `VehicleLookupBackfillTests` with exit code 0; retain any first nonzero result and all subsequent exact exits.
- Done when: the exact diff contains only the two historical columns and their two true values in the shared `Cases` insert, pending parent-supplied verification evidence and independent simplification.
- Deviation stop: stop if the target schema does not require both columns, the change needs another file, a test assertion fails, verification requires local execution, or the diff includes any unrelated path.

## Acceptance checks

- The three shared setups can supply every non-null `Cases` field required at the test's selected migration target.
- Existing assertions still prove migration output, fact precedence, and a second no-op application; none is weakened or removed.
- The diff is limited to `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs`; no production caller, runtime artifact, schema/grant, or deployment evidence applies.
- Parent/sole verifier retains original nonzero results (if any), requests the focused class filter, and reports exact exit evidence before a commit, push, PR, or review hand-off.
- Independent simplification confirms the one-line seed correction is the smallest sufficient diff before commit/PR authorization.

## Commands

Do not run commands locally in this pass. The designated verifier may run a sequential affected Release build if required, then:

```powershell
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~VehicleLookupBackfillTests"
```

## Failure and deviation rules

Stop and report any scope expansion, target-schema contradiction, dirty path in this ticket worktree, failed or unavailable verifier evidence, dependency addition, or request to alter assertions. Do not substitute a current-schema fixture, remove a migration target, or improvise a production-schema change.

## Stop condition

After the bounded fixture edit and checklist update, stop in Implementing with the exact diff and test filter reported to the parent. Do not run verification, write a completion report, commit, push, open a PR, merge, review, or start another ticket until the parent supplies verifier results and independent simplification.
