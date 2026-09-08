---
kind: review-attestation
pr: "700"
head_sha: "2cde68831485bfc426e062a83030edea661f976d"
verdict: pass
reviewer: "principal_delivery_audit"
independent: true
plan_hash: "07f29f3b2342f40f"
ticket_updated: "2026-09-08T15:12:43.981Z"
board_sha: "34bbc3e8fe642a76b84cf28ef1d3d17bd4609880"
expected_reviewers:
  - "principal_delivery_audit"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Optional assessment dictionary absorbed unrelated Case form keys."
    disposition: fixed
  - id: F-002
    severity: major
    summary: "Save authorization refusal discarded proposed Case values."
    disposition: fixed
  - id: F-003
    severity: major
    summary: "Carried unshown accepted Facts or equal Confirmed values lost their original provenance."
    disposition: fixed
  - id: F-004
    severity: major
    summary: "Two existing Case-page fixtures were not adapted to the new canonical metadata and accepted-settlement readers."
    disposition: fixed
  - id: F-005
    severity: note
    summary: "Manual multi-width visual acceptance remains inconclusive."
    disposition: accepted-risk
    reason: "Root-approved integration-first sequencing only; actual 1580/1100/760 editable, read-only and conflict visual acceptance remains mandatory in Verifying and blocks Done and release acceptance."
---

# ENG-029 independent delta review

## Decision and immutable inputs

PASS for the approved source/runtime integration-first boundary at exact PR head
2cde68831485bfc426e062a83030edea661f976d. No merge, deployment, proof, Done,
or visual-acceptance claim is made.

This is review round 1 and therefore a bounded delta review. It carries forward
F-001 through F-005 from consolidated review e636932136f144cc and examines
F-004, the remediation lines since the previously attested head, their direct
production contracts, the two affected callers, current packet versions, the
current PR diff, integration resolution, checks, comments, reviews and threads.
No unrestricted re-audit of unchanged source was performed.

The reviewer remains independent of the implementation authors and is the only
expected reviewer named by the controller. The reviewer settled on this exact
head through public review 5143882049:
https://github.com/collisionengineers/pegasus/pull/700#pullrequestreview-5143882049.
The prior needs-changes review 5138568230 remains historical evidence on
f86054c0e7cc73cb6245355dd21c03e58196d582.

At the final gather the ticket remains in Review at review round 1, timestamp
2026-09-08T15:12:43.981Z and revision rev1:3914d17379791100. The plan version
is 07f29f3b2342f40f. Review-stage gates are satisfied. The author worktree is
clean on ENG-029-case-workspace-editors, and its local head and remote branch
both equal the attested SHA. The pushed board tip is the board SHA above with
zero ahead and zero behind; ENG-029 ticket and plan bytes are unchanged there.
Unrelated active board writes do not alter this ticket snapshot.

GitHub reports the PR OPEN, ready, mergeable and UNSTABLE because non-required
checks failed. It targets dev and contains 28 current paths. GitHub reports no
required checks; dev branch protection is absent and the effective branch-rule
list is empty. All paginated reviews, issue comments, inline comments and
GraphQL review threads were re-read after the public delta review. There are no
inline comments and no review threads, so the thread snapshot is truthfully
empty.

## Delta and integration review

The remediation commit fef8909e2c256388c4d738ddf78f176e0e1b5cef changes
exactly the two planned test fixtures.

In AssessmentEstimateImportWebTests, RecordingStores.SetCurrentEstimate now
takes the existing candidate, uses EstimateTotals.Compute and
EstimatePolicy.BasisFor only when that candidate is Draft, records those
frozen values, and then marks it Accepted/current. An already Accepted
candidate retains its recorded breakdown. This matches the real
EfRepairSpecificationStore acceptance contract, preserves the single totals
owner, and does not weaken ReportRepairCosts or EstimateTotals.ForProjection.

In AssessmentReadinessSummaryBrowserTests, the existing fake now implements
ICaseReportSnapshotSource, returns metadata-only CaseReportFreezeInputs with no
fabricated ready facts, and counts metadata reads. The Case, workflow,
assessment workspace and access fixture consistently use ReportPreparation.
The existing assertions for named Not ready conditions, disabled generation
and preview, absence of an actual preview, full Case shell, empty estimate
state and accessibility remain. The added MetadataReads assertion proves that
the production page uses the intended port.

The current head is a normal merge of that remediation commit with accepted
dev a1f0bfe260ea05df531df6e0ca3109141e7697da. The current PR diff against that
base contains the original bounded ENG-029 work plus these two fixtures. The
merge's only manual content resolution keeps dev's current design README
unchanged and therefore removes that file from the PR diff, while retaining
only ENG-029's narrow Settlement/Report workspace-Save paragraph in dev's
rewritten FRD-11 Report-generation section. FRD-06 merged cleanly. Current
documentation checks and link checks pass. No conflict marker, machine path,
new dependency, production fallback or compatibility path was introduced.

## Findings and dispositions

F-001 remains fixed. The optional assessment dictionary is explicitly bound to
its named form prefix and cannot absorb unrelated Case form keys.

F-002 remains fixed. Authorization refusal retains proposed Case values while
clearing edit authority and preserving the no-write result.

F-003 remains fixed. Equal accepted Fact or Confirmed values retain their
existing provenance; suggestions are not silently promoted.

F-004 is fixed. Both observed existing callers now implement the production
contracts described above. The correction is one root-cause remediation class,
not a production fallback or a weakened test.

F-005 remains accepted risk only at the explicitly approved milestone boundary.
Manual 1580/1100/760 inspection of editable, read-only and conflict states is
still INCONCLUSIVE because the prior local-file browser attempt was refused by
browser security. Automated 1440 capture does not substitute for it. The
obligation stays unchecked in Verifying and prevents Done and release
acceptance until a genuine visual PASS exists.

## Verification evidence and retained failures

Root's exact merged-head focused verification is preserved in scratch/verify:
the two named F-004 cases passed 2/2 after the incremental Integration build.
No test or build was run by this reviewer.

Exact-head GitHub Actions run 34243221185 completed FAILURE and is not relabelled
green:

| Job | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | --- |
| unit | 1928 | 1 | 14 | FAILURE |
| SQL shard 1 | 673 | 4 | 1 | FAILURE |
| SQL shard 2 | 616 | 29 | 3 | FAILURE |
| SQL shard 3 | 620 | 15 | 0 | FAILURE |
| browser | 134 | 1 | 0 | FAILURE |
| Test-UI capture | 134 | 1 | 0 | FAILURE |

Every job's common Release build succeeded. Changes, documentation,
local-development-scripts, reference-data and SQL coverage succeeded;
infrastructure was path-skipped.

The retained shard-1 artifact proves all 21 AssessmentEstimateImportWebTests
ran and passed, including UseEstimateRecordsTheEngineersAcceptance. The only
four SQL-shard-1 failures are the three historical VehicleLookupBackfill cases
and TestUiFocusedRenderTests.HeldLeaseConfirmationClearsOnlyTheCurrentLeaseThroughRazor.
Neither F-004 case failed. Both 135-case browser executions have only the
UploadCaseSearch failure, so
NotReadyReportDraftControlsStateTheConditionAndTheShellRenders passed in both.
Compared with the consolidated-review run, the exact F-004 failures have moved
from one SQL failure plus the same readiness failure in both browser lanes to
three exact passes.

Remaining failures are preserved as separately owned baseline work, not
absorbed into ENG-029 and not waived. The stale source-inventory failure is
owned by INTK-065; accepted-intake, null-allocation and the current
InaccessibleCaseCannotPostSendToClaude baseline correction are owned by
DELIV-056; the historical VehicleLookup fixture correction is DELIV-057; and
the upload/attach contract contradiction is INTK-066 pending operator
clarification. Other intake cascades remain with their existing owners. Some
corrections have since integrated to dev, but this record does not claim a
fresh merged-source CI pass.

The automated security status comment applies to the older f86054c head only
and is not presented as exact-head bot evidence. The current remediation delta
is test-fixture-only; independent source review found no unresolved security,
data-loss or destructive risk. There are no required checks or open findings.

## Recommendation and stop

The exact head satisfies the approved plan and bounded delta review. Recommend
ordinary squash integration into dev only if root, after reading this record,
grants merge authority and performs the review skill's final fresh
head/check/thread/board gather. Preserve the non-green CI record and F-005
visual obligation. This reviewer stops in Review with no merge or stage move.
