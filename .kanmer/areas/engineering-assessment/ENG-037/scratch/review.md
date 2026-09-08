---
kind: review-attestation
pr: "695"
head_sha: "7c86e9bc91f12f03a37a9a7c33dd1f5afcf2e524"
verdict: pass
reviewer: "/root/intake_audit"
independent: true
plan_hash: "7acbd02e0af054f3"
ticket_updated: "2026-09-08T03:13:33.468Z"
board_sha: "fddd4e84ae8460e6dd488a32b573c8405c9ecc75"
expected_reviewers:
  - "/root/intake_audit"
threads_snapshot: []
findings: []
---

# Independent review — ENG-037

## Decision and inputs

PASS on the exact head above. Author is /root; reviewer is the separately
assigned /root/intake_audit role. Sharing the GitHub account does not make
this self-review. The reviewer authored none of these changes and did not
run builds or tests.

The sole expected reviewer settled with exact-head public comment review
5136969812 at 2026-09-08T03:19:15Z:
https://github.com/collisionengineers/pegasus/pull/695#pullrequestreview-5136969812

Read the complete two-file diff, current ticket and all packet documents:
files f24bccc84a440a9e, plan 7acbd02e0af054f3,
checklist 3a235bf92e61d944 (2/2), report 9c85672f44891b2c,
and execution scratch. No research or open questions document exists.
Review round is the initial consolidated round (no recorded return).
Read governing FRD-06/11 and EPIC-012, inherited EPIC-011 context/waves and
EPIC-008 context. EPIC-014/current instructions govern focused verification
and the single heavy verifier rather than historical wave-wide commands.

The historical ticket text discusses two faulty call sites; its current
residual section and plan correctly identify that the report half was
already fixed on accepted dev. This PR changes only the remaining normalizer.

## Source and acceptance

The existing DateOnly.TryParseExact call in AssessmentPolicy.NormalizeValue
now explicitly uses CultureInfo.InvariantCulture and DateTimeStyles.None.
Exact yyyy-MM-dd input, DateOnly.MinValue rejection, errors and invariant
serialization remain intact. SaveAssessment.ExecuteAsync and
EfCaseAssessmentStore.SaveAsync are existing production normalization callers;
there is no new policy, wrapper, dependency, UI, schema or provider boundary.

The three-culture theory exercises existing writable date vocabulary under
th-TH, ar-SA and en-GB. It proves ordinary and leap-day values remain exact,
rejects invalid dates, alternate formatting and the minimum date, and restores
CurrentCulture in finally. The values are explicitly calendar probes, not
fabricated case evidence. Existing report projection uses the invariant
overload and its unchanged th-TH test covers persisted dates.

The clean author worktree and full PR diff contain exactly the two approved
files (+43/-1). Read-only diff whitespace check passed. No source changes
were made by the reviewer.

## Runtime evidence examined

Root report records locked Core/test restore PASS, focused Release build
PASS in 17.14 seconds with zero warnings/errors, and 78 focused
AssessmentPolicyTests/AssessmentReportProjectionTests PASS in 129 ms.
The reviewer independently read the retained TRX counters: 78 total,
78 executed, 78 passed, zero failed, notExecuted, inconclusive or skipped.

Artifact: artifacts/verification/eng-037-dates.trx in the author worktree.
SHA256:
E70AEAE038C0D62BEDD05104026ACCC5BBC6D2F024B16F4B5227231CBBD3B4D1

These are reused author/runtime evidence, not a new reviewer execution.
No full-solution, CI, merged verification or deployment pass is claimed.

## Checks, threads and dispositions

Fresh GitHub gather after the public review retained exact head, OPEN PR,
base dev, the public COMMENTED review and no review threads.
statusCheckRollup is empty. Effective dev branch rules return []; classic
branch protection returns HTTP 404, Branch not protected. Therefore no
configured required check was absent/red, but empty checks are not CI PASS.

Automated issue comment IC_kwDOThBrk88AAAABTIHKqg (5578541738) is completed
informational security-review status with mergeGateEnabled=false and no
findings. Disposition: no action required because it makes no source finding;
it is not an expected reviewer or a review thread. The public independent
review also states that disposition. There are no unresolved findings,
threads or material residual risks within this bounded correction.

The reviewed board tip was pushed (ahead 0, behind 0). Local custom-skill
drift warnings were observed as existing setup state; this review used the
repository skill and did not mutate unrelated setup.

## Final authorized merge handoff

Root subsequently delegated the normal guarded squash merge to this independent
reviewer under the user's full merge permission, after CASE-031 PR694 merged
at 3c1d04781719f92c86a190f510c1924fc9df6328 on 8 September 2026 at 03:27:01Z.
This serializes changes to the integration branch. It is not self-merge.

The final gather retained the same plan version, ticket timestamp, author head,
public independent review, completed informational bot comment, empty review
threads and empty check/rule set; protection remains HTTP 404. Full two-file
diff is unchanged. The new pushed board tip is recorded above.

The reviewer will use a normal squash merge with the exact head guard and
[skip ci] subject, without admin bypass or branch deletion. A failed merge
leaves Review unchanged. After GitHub confirms the merge, fresh doc gates
precede the single Review to Verifying move. Root owns exact merged-source
verification; this review is not proof. No reviewer build/test, source,
lease, unrelated metadata or workspace changes are authorized.
