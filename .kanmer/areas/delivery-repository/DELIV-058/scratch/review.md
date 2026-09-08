---
kind: review-attestation
pr: "708"
head_sha: "71c1bc1266583459d40b82c3d19d59af632afa7d"
verdict: pass
reviewer: "/root/parallel_plan_review"
independent: true
plan_hash: "128e84dc19caa5f6"
ticket_updated: "2026-09-08T15:20:07.226Z"
board_sha: "800b09a0ab3b0246e144ee5e92c2eef1c5c9e462"
expected_reviewers: ["/root/parallel_plan_review"]
threads_snapshot: []
findings:
  - id: F-001
    severity: note
    summary: "The optional unit lane used the pre-INTK-065 dev merge base and failed the stale principal source-inventory test before its architecture subprocess ran."
    disposition: deferred-to-ticket
    ticket: INTK-065
  - id: F-002
    severity: note
    summary: "The optional SQL shard 1 exposes three historical vehicle-lookup fixtures that omit InstructionConfirmedByStaff."
    disposition: deferred-to-ticket
    ticket: DELIV-057
  - id: F-003
    severity: note
    summary: "The optional SQL shards expose intake fixtures that now route NeedsSorting or produce no IntakeAllocationState."
    disposition: deferred-to-ticket
    ticket: DELIV-056
  - id: F-004
    severity: note
    summary: "The optional browser and Test UI lanes repeat the upload case-search timeout."
    disposition: rejected-with-reason
    reason: "PR 708 changes only two architecture-test files and no upload, Web, browser, runtime or integration-test path; both lanes failed the same unchanged test while 134 of 135 tests passed."
  - id: F-005
    severity: note
    summary: "The optional SQL shard 3 exposes the stale InaccessibleCaseCannotPostSendToClaude gated-disabled-control expectation."
    disposition: deferred-to-ticket
    ticket: DELIV-056
---

# DELIV-058 independent review

## Inputs and independence

The reviewer is the separately assigned reviewer role
`/root/parallel_plan_review`; the author role is `correction_ownership`.
This is an independent round-0 whole-PR review. PR 708 was reviewed at exact
head `71c1bc1266583459d40b82c3d19d59af632afa7d` against plan version
`128e84dc19caa5f6`, ticket revision `rev1:3488e8e52112c560`, and ticket
timestamp `2026-09-08T15:20:07.226Z`. The pushed board tip was
`800b09a0ab3b0246e144ee5e92c2eef1c5c9e462` with ahead 0.

The complete packet, all three governing refs, exact PR diff, implementation
report, sole-host verifier record, current checks, reviews, comments and review
threads were gathered. The ticket is in Review, PR 708 is open and ready,
mergeable, targets the configured integration branch `dev`, and retains the
expected single commit. No group context applies. GitHub reviews and review
threads are empty. The expected reviewer set is settled by this record.

## Changes and scope reviewed

The PR modifies exactly the two declared architecture-test files: 14 insertions
and 4 deletions. Their blobs are
`77ca689ea1d91d694429cdcdee74ffd4ab29daf4` and
`80b74ef998d4ba905cd4b6dc05b10cea1945cd87`, matching the frozen handoff and
clean ticket worktree.

`DependencyDirectionTests` now requires `ProcessIntake` to consume the
existing `InstructionExtractionPolicySelector`, rejects every constructor
parameter assignable to `IInstructionExtractionPolicy`, and requires the
selector's sole constructor to consume
`IEnumerable<IInstructionExtractionPolicy>`. It retains the Core-only and
no-Infrastructure implementation assertions. This protects both directions of
the current FRD-09/Core ownership boundary without retaining the obsolete
direct-policy contract.

`StagedArtifactReconciliationFunctionTests` retains exact ordered constructor
equality and inserts `IImageIntakeCasePairing` and `ITriageCasePairing` in
the same positions as the live Worker constructor. This preserves the FRD-02
scheduled reconciliation composition. No `src/**`, runtime, registration,
constructor, policy, schema, dependency, documentation, release, D56 or
INTK-002 path changes.

## Acceptance and direct verification evidence

The independent static comparison confirms every planned acceptance check:
selector presence, absence of any assignable direct/concrete extraction policy
parameter, explicit selector collection ownership, exact Worker parameter
order including both pairings, and only the two expected files.

The designated sole-host verifier bound its evidence to this plan and frozen
diff. Locked restore exited 0; the Release no-restore build exited 0 with zero
warnings and errors; the two focused architecture tests passed 2/2; and the
full architecture project passed 116/116. Its postcheck retained the exact two
files, clean diff check and both approved blobs. This review ran no build or
test and does not claim a broader application-suite PASS.

## GitHub checks and external inputs

The exact query immediately before this record reported no required checks on
`DELIV-058-architecture-assertions`. Absence is configuration evidence, not
a fabricated green suite. The broader optional repository-check run
`34244034556` completed FAILURE and remains explicit non-PASS evidence.

Changes, documentation, local-development-scripts, reference-data and
sql-integration-coverage passed; infrastructure was path-skipped. Unit,
browser, Test UI and all three SQL shards failed. Those actual failures are
dispositioned without relabelling the broader run green:

- F-001: unit built successfully, then Core passed 1,906 and skipped 14 before
  `TrackedPegasusSourceHashesHaveNotDrifted` failed on the deleted
  `QdosCaseMatchPolicy.cs` path. The `&&` architecture subprocess therefore
  did not run in this job. INTK-065 owns that source-inventory correction and
  has since merged to `dev`; this PR's exact-head 116/116 verifier result is
  the direct architecture evidence.
- F-002: SQL shard 1 passed 658, skipped 1 and failed three vehicle-lookup
  fixtures because they inserted NULL `InstructionConfirmedByStaff`.
  DELIV-057 owns that fixture class and has since merged to `dev`.
- F-003: SQL shard 1's remaining failure expected `CaseCreated` but observed
  `NeedsSorting`; shard 2 passed 612, skipped 3 and failed 29; shard 3 passed
  620 and failed 15. Except for F-005, those shard-2/3 failures are the same
  fixture class: accepted allocation was expected while current evidence
  routed `NeedsSorting` or produced no `IntakeAllocationState`. DELIV-056
  owns the evidence-fixture correction.
- F-004: browser and Test UI each passed 134/135 and failed the same
  `UploadCaseSearchBrowserTests` case after a 30-second wait for
  `details.upload-attach > summary`. The PR changes no causal path, so this is
  rejected as a PR-708 finding without being relabelled PASS.
- F-005: SQL shard 3's
  `InaccessibleCaseCannotPostSendToClaude` failed its historical
  gated-disabled-control expectation. Current read-only cases omit mutation
  controls; DELIV-056 owns the stronger negative-absence correction alongside
  its related seed correction. No ENG-034 remediation or new ticket is
  inferred.

The corrective programme still owes a converged full repository gate before
promotion.

The sole GitHub comment is Codex status
`IC_kwDOThBrk88AAAABTQrlSw`, bound to this exact head. Its security review
completed at `2026-09-08T15:24:57.961887Z`, produced no suggestion, review or
thread, and reports `mergeGateEnabled: false`. It is informational and has no
finding to disposition.

## Decision and residual risk

F-001 through F-003 and F-005 are terminally deferred to their named owners.
F-004 is rejected as causal to this two-test diff for the stated path and
baseline evidence. No finding remains open. There is no unmet scoped acceptance
check, review thread, security/data-loss/destructive risk, runtime change, or
failed/missing required check.

PASS for the bounded DELIV-058 architecture-assertion correction at the exact
head above. This does not claim the optional repository workflow is green and
does not accept release, deployment, proof, source edit, rerun or cleanup.
Root has supplied explicit authority for ordinary squash integration to
`dev` only. Merge remains contingent on a fresh unchanged
head/check/thread/ticket/plan/board gather and permits no force, bypass, waiver
or `main` promotion. Exact merged-SHA verification and Done remain separate
obligations.
