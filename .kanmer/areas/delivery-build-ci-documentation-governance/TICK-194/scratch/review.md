# Independent review — PR #377

Reviewer: independent from implementation.

## Changes

- `.github/workflows/ci.yml`: invokes the guard only for `push` events on `refs/heads/main` after full-history checkout, and classifies guard-script changes as build-relevant.
- `scripts/Test-MainBranchHistory.ps1`: resolves explicit before/head commits, rejects the zero sentinel, unavailable/non-ancestor/empty ranges, and requires exactly two parents for every new first-parent commit.
- `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs`: adds six synthetic-history tests for the allowed merge append and direct, mixed, missing, zero, and rewritten rejection cases.
- `docs/temp-plans/main-branch-history-guard.md`: records ticket scope, owned files, acceptance, and the UI-revamp exclusion.

## Comments and disposition

- Non-blocking — the control is post-push detection, not prevention. Disposition: won't-do-because branch protection/rulesets and recovery are explicitly outside TICK-194; the plan, script output, and report are accurate about this boundary.
- Non-blocking — local full architecture execution is 92/93 because `WorkerActivationReleaseContractTests.LocalDeploymentPlanRejectsAppendedRogueHardCodedWorkerSetting` fails. Disposition: won't-do-because the failure reproduces independently at the pre-existing `The Web Container App must scale only from zero to one replica` assertion, before the intended rogue-setting census, and PR #377 changes none of the test, script, Bicep, parameter, YAML, or production-smoke fixture inputs.
- Non-blocking — `actionlint` was unavailable locally. Disposition: GitHub parsed and started the workflow successfully; the repository-check `changes`, `documentation`, and `reference-data` jobs passed. Remaining required jobs are still pending and remain the merge gate.
- No blocking findings. No follow-up ticket required.

## Plan and implementation completeness

The ticket requirement, EPIC-001 context, research/files documents, plan/checklist, and post-implementation report are mutually consistent. The plan does not miss work implied by the ticket. The implementation does not miss planned work. The report honestly lists every changed file and its rationale. No governing PRD/FRD/ADR applies: this enforces existing repository process in `AGENTS.md` and `docs/engineering.md` without changing product behaviour or application architecture.

The exact reviewed head is `5599899c43086c46586eb60edc7372098f80e374`. The diff is limited to the four planned paths and contains no `src/Pegasus.Web/**`, UI browser/snapshot, `design/**`, or `.stitch/**` path.

## Verdict

PASS on plan completeness, implementation completeness, correctness, focused verification (6/6), report accuracy, governing-doc alignment, and UI non-overlap. Merge is deferred until every required GitHub check is green; TICK-194 remains in Review meanwhile.

## CI gate update

Required GitHub checks are not green, so PR #377 was not merged and TICK-194 was not moved. At the latest review poll: `unit` failed, `qdos-pressure` failed, `browser` and all three `sql-integration` shards remained pending; `changes`, `documentation`, and `reference-data` passed. GitHub withheld failed-job logs while the workflow run remained in progress, so no unsupported attribution is made for those two CI failures.

# Independent re-review — refreshed PR #377

Reviewer: independent from implementation.

## Changes

- Exact head `740425144f73197371c7532034f951602898cbef` is a clean two-parent merge of original reviewed head `5599899c43086c46586eb60edc7372098f80e374` and refreshed `origin/dev` at `14ce3843ddb63610a19de406bfaa153721b2d313`.
- Relative to current `origin/dev`, the PR still changes only `.github/workflows/ci.yml`, `scripts/Test-MainBranchHistory.ps1`, `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs`, and `docs/temp-plans/main-branch-history-guard.md`.
- The implementation behavior and six synthetic history scenarios are unchanged from the prior passing review.

## Comments and disposition

- Non-blocking — the refreshed branch incorporates DELIVE-001's CI hardening. Disposition: fixed-in-PR through a normal merge from `origin/dev`; no unrelated DELIVE-001 content remains in the PR-relative diff.
- Non-blocking — GitHub reports a Node.js 20 deprecation annotation for `actions/cache@v4` in the browser job. Disposition: won't-do-because it is existing workflow dependency maintenance outside TICK-194 and did not fail the check.
- No blocking findings. No follow-up ticket required.

## Completeness and UI boundary

The plan remains complete for the ticket, and the refreshed implementation still completes the plan. The post-implementation report accurately records the DELIVE-001 refresh and all four PR-relative file changes. The existing governing-doc conclusion remains valid. The diff contains no `src/Pegasus.Web/**`, UI browser/snapshot, `design/**`, or `.stitch/**` path.

## CI evidence

GitHub Actions run `31995971485` completed successfully at the exact reviewed head. Green jobs: `changes`, `documentation`, `reference-data`, `unit`, `browser`, all three `sql-integration` shards, and `sql-integration-coverage`.

## Verdict

PASS at exact head `740425144f73197371c7532034f951602898cbef`. Plan completeness, implementation completeness, report accuracy, governing-doc alignment, UI non-overlap, clean base refresh, and all required GitHub checks are confirmed.
