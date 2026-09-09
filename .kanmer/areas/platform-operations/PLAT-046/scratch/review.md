---
kind: review-attestation
pr: "713"
head_sha: "a2bb0e120575d46828911d15031e7a36dc1ff4de"
verdict: pass
reviewer: "/root/review_merge_711"
independent: true
plan_hash: "8c4cdb7b3a400d66"
ticket_updated: "2026-09-09T01:25:07.073Z"
board_sha: "ec0ed5f0ef565604f7dbfb37ff7c3cf273a1c956"
expected_reviewers: ["/root/review_merge_711"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "PR711 omitted the affected architecture literal-census test consumer."
    disposition: fixed
    reason: "Corrected by a2bb0e120575d46828911d15031e7a36dc1ff4de; exact census and both consumer wiring are asserted without weakening existing safety tests."
  - id: F-002
    severity: major
    summary: "Pre-confirmation upload tests fail outside this architecture-only corrective PR."
    disposition: deferred-to-ticket
    ticket: INTK-066
    reason: "Existing PR712 owns manual-upload confirmation behavior and operator-authorized browser/snapshot removal; PR713 changes only the Worker architecture test."
---

# Independent corrective review — PLAT-046 / PR713

The distinct author is /root/remediate_711_contract. Sole expected reviewer
/root/review_merge_711 settled at the exact head in
[public review](https://github.com/collisionengineers/pegasus/pull/713#issuecomment-5594453423).
GitHub review-thread census is empty. Automated security summary reports no
findings and is not an expected reviewer.

## Inputs and scope

Read live ticket, amended plan/files/checklist, implementation report
8f0043dc56ba1741, original research and governing release/runbook owners,
and retained FAIL proof dc4ca8507b568ff3. No group/open-question is attached.
Reviewed the whole corrective PR: exactly one test file, 13 additions/3 removals,
against integrated dev c3219cd28c69530441e2bba7357063372628ff37.
This is the bounded post-merge correction, not a renewed unrestricted feature
audit. Production scripts, dependency graph and release policy are unchanged.

## F-001 — fixed

The previous consumer searched Invoke-ProductionSmoke.ps1 for literals moved
by PR711 into Get-PegasusWorkerDisabledSettingNames. Its empty result was
deterministic. The new test reads the canonical producer, retains ExpectedFunctions,
and proves both smoke and deployment-plan callers dot-source and use that helper.
Every prior unsafe-disable assertion and runtime missing/extra/duplicate/case/
malformed/value rejection fixture remains. Existing test infrastructure is reused.

The original [PR711 review](https://github.com/collisionengineers/pegasus/pull/711#issuecomment-5594102721)
missed this affected consumer. Its claimed completeness was invalidated by the
subsequent exact c3219cd post-merge FAIL; that proof and history are retained,
not relabeled PASS. The corrective commit fixes that one root-cause class.

## Required evidence and CI classification

Amended plan acceptance requires the formerly failing test and existing
WorkerActivationReleaseContractTests safety evidence. Sole verifier recorded
locked restore, Release architecture build with zero warnings/errors, all
116 architecture tests PASS (including all 24 Worker activation tests),
Test-PegasusPlatform and Local Test-AzureDeploymentPlan PASS on this exact head.
Reviewer git diff --check passed. No reviewer build/test was run.

[CI run34298650052](https://github.com/collisionengineers/pegasus/actions/runs/34298650052)
has unit, changes, documentation, local-development-scripts and reference-data
PASS. Infrastructure is scope-skipped. The absence of a GitHub protected dev
branch (404), active branch rules ([]), and required checks was read directly.
Absence of protection is not a waiver of the plan: those actual architecture
and script obligations are satisfied. Current AGENTS and engineering verification
policy require effect-proportional checks and treat the classifier as a routing
aid; the broad tests/** Build flag selects unrelated suites but does not expand
this one-file correction's approved acceptance.

## F-002 — deferred to [[INTK-066]]

Actual failures, not PASS or presumed flakes:

- [test-ui job102300801076](https://github.com/collisionengineers/pegasus/actions/runs/34298650052/job/102300801076):
  UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision
  timed out after 30000ms waiting for details.upload-attach > summary.
  Capture phase had 134 pass/1 fail and aborted; this is not snapshot drift.
- [SQL shard3 job102300801071](https://github.com/collisionengineers/pegasus/actions/runs/34298650052/job/102300801071):
  UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely
  could not find "No existing case matched this"; 634 pass/1 fail.

Both concern unchanged upload decision paths outside the Worker architecture
correction. Existing INTK-066/PR712 owns the manual-confirmation behavior and
explicitly authorized browser/snapshot retirement. No workaround, test deletion,
waiver or source absorption into PR713 is authorized or performed here.
Supporting INTK-066 evidence: the exact named UploadConfirmationWebTests method
passed in the current 0172 TRX in 00:00:05.1543188. Commit
da6ff8e16815100e42da65e60df3e45a3cced2d2 changes the production manual-destination
workflow and its assertions to "Choose a case destination", retaining actual
association, exact Case URL, no automatic association and replay/version checks.
This supports deferral to its existing owner, not a wording-only waiver.
Remaining SQL1/2 and browser jobs were pending at gather, NOT PASS; they are
nonrequired evidence outside this ticket's acceptance. Any newly revealed
in-scope risk before merge requires fresh disposition.

## Decision and handoff

PASS for the exact bounded corrective PR; no open findings or unmet scoped
acceptance. This does not mean all CI jobs are green or the old UI behavior is
accepted. The operator authorized the necessary corrective merge to dev as
part of completing PR711. Confirm remote board contains this record and
re-gather head/plan/ticket/checks/threads immediately before merge.
After merge, move only Review to Verifying; kanmer-verify owns exact corrective
merge-SHA proof. No main promotion, cloud write, deployment or code edit occurred.
