# Six-shard SQL CI progress

Snapshot: 16 September 2026, approximately 12:58 UTC / 13:58 BST.
Temporary operator-requested handover. See the [approved plan](PLAN.md).

## Overall status

Source changes are prepared in the performance-next worktree. They are
uncommitted and unpushed. CI/scripts/runbook changes passed independent static
review. The test split has author-reported static preservation evidence but
has not yet had independent complete-diff review, a build, runtime discovery
or test execution. No six-shard CI run has started.

Implementation was paused to produce this sprint handover at the operator's
request. No local workload is running for this task.

## Exact source and PR state

| Item | Snapshot |
| --- | --- |
| Worktree | `C:/Users/Alex/Documents/GitHub/pegasus-worktrees/performance-next` |
| Branch | `perf/first-use-and-image-cache` |
| Local committed HEAD and remote PR head | `e4fc0ea05762aab67dd6ec3c8aaf35a280d5f2aa` |
| PR | [#764](https://github.com/collisionengineers/pegasus/pull/764), open, targeting `dev` |
| Pending source delta | 20 modified tracked files and one new support file |
| Last CI | [35083151345](https://github.com/collisionengineers/pegasus/actions/runs/35083151345), completed with timeouts; not green |

The pending delta is four CI/script/documentation files, 16 existing Case
test/support files and new `CaseWebTestSupport.Recording.cs`. No application
source has been changed by this CI-remediation slice.

## Completed implementation

| Work | State and evidence |
| --- | --- |
| Six matrix entries | Prepared in `.github/workflows/ci.yml` |
| Execution and partition count six | Both updated consistently |
| Existing limits and semantics | 45-minute timeout, max-four class concurrency, corpus exclusion and failure/artifact handling retained |
| Shard regression expansion | Prepared for 3/6 runners, reversed input, whole classes, exact coverage and sparse assignments; not executed |
| Runbook and examples | Updated; static review approved |
| Concrete feature classes | 15 classes with explicit SQL traits and static support imports |
| Main-file decomposition | 38 test methods moved to feature owners; eight methods remain in Details |
| Shared support | Internal static partial helper owner; nested stores/workspaces remain per-test |
| Static preservation | Author reports 127 attributed methods and 188 parameterized rows preserved, no missing/extra/duplicate methods, matching normalized body/attribute hashes |
| Whitespace | Source `git diff --check` clean; CRLF conversion advisories only |

The exact concrete classes are:

- `CaseAssetPreparationWebTests`
- `CaseClosureWebTests`
- `CaseCustodyWebTests`
- `CaseDamageAndViewerWebTests`
- `CaseDetailsWebTests`
- `CaseEditModeWebTests`
- `CaseEstimateHeaderWebTests`
- `CaseRecordFrameV26WebTests`
- `CaseRecordGapsV26WebTests`
- `CaseReportApprovalWebTests`
- `CaseTasksWebTests`
- `CaseValuationV26WebTests`
- `CaseValuationWebTests`
- `CaseVehicleWebTests`
- `CaseWorkflowWebTests`

One grouping detail to check in review: the author placed
`SendPageRendersItsChoiceInReviewAndWithEngineer` in Custody, whereas the
approved plan grouped lifecycle/EVA into Workflow. Confirm its actual feature
owner; this is not a claim that a test changed or was lost.

## Why this work was selected

| Prior SQL job | Outcome |
| --- | --- |
| Shard 1 | Job timed out at 45m01s; all 860 assigned rows completed: 858 passed, two skipped, no failed assertions. Test duration 39m41s; job is still failed. |
| Shard 2 | Job timed out at 45m07s while testing 757 assigned rows. No completed TRX; incomplete, not passed. |
| Shard 3 | Passed in 16m47s; 732 rows passed; test duration about 12m20s. |
| Partition gate | Skipped because the upstream run was cancelled; no full-suite green verdict. |

Retained shard-1 TRX shows the old CaseDetails class ran serially for
31m07.255s, approximately 78.5% of that shard's test duration. Splitting by
file alone would still leave 92 rows and 15.78 minutes in the main file,
which is why those methods were redistributed too. These are observations
under the old run's contention, not guaranteed isolated-run lower bounds.

## Outstanding work, in order

1. Independently review the full test split and shared helper accessibility,
   imports, per-test state and absence of inherited/duplicated tests.
2. Correct any source issues, then commit a frozen candidate.
3. Obtain a fresh host-slot grant and execute the prepared local verification
   sequence in the plan. Prior test runs do not qualify for these new bytes.
4. Confirm actual runner discovery preserves all 2,349 non-corpus rows and
   the exact 188 moved Case rows. Static counts are not discovery evidence.
5. Run the 15 classes together, plus shard regression and documentation checks.
6. Push the candidate to PR #764, refresh its evidence and wait for full CI.
7. Require all six shards and the partition gate to pass; publish timings.

No merge, release packaging or deployment is part of this completed slice.
The earlier proposal to increase the timeout to 60 minutes was superseded by
the agreed split-plus-six decision and was not implemented.

## Evidence and coordination pointers

Paths below are local/private working evidence, not portable PR attachments.
Repository-relative artifact paths refer to the original Pegasus checkout.

- Method/attribute/body mapping:
  `C:/Users/Alex/Documents/GitHub/pegasus/.git/worktrees/performance-next/caseweb-test-split-map.json`.
- Prior discovery, assignments and completed TRX files:
  `artifacts/performance/implementation-20260916/ci-timeout-artifacts/`.
- Timeout logs: `ci-shard1-timeout.log` and `ci-shard2-timeout.log` in that
  implementation evidence directory.
- Task context: `artifacts/performance/implementation-20260916/context.json`.
- Canonical host slot:
  `artifacts/performance/operator-20260915/host-slot.json`.

The slot was initially held by the separate case-linking task. Its latest
read-back is explicitly idle; the performance task has a queued request, not
a grant. Re-read current state and check other execution contexts/processes
before claiming it. Do not treat this snapshot as permission to run.

The existing PR body predates this remediation and still describes the old
CI as pending. This report records its actual completed timeout outcome;
updating the PR remains a follow-up, not an action taken for this handover.
