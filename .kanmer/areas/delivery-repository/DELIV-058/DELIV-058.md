---
id: DELIV-058
type: ticket
title: >-
  Align architecture assertions with current extraction selection and pairing
  composition
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T14:58:28.071Z'
  review: '2026-09-08T15:20:07.226Z'
  verifying: '2026-09-08T15:47:47.145Z'
  done: '2026-09-08T15:59:59.620Z'
taken_at: '2026-09-08T15:09:15.962Z'
branch: DELIV-058-architecture-assertions
worktree: .worktrees/deliv-058
claim_expires_at: '2026-09-08T16:18:25.204Z'
claim_controller: codex-mcp-client
lease_id: 238978fe-9448-4810-8709-bb4a4db3b019
lease_revision: 3
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-058'
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T15:48:25.204Z'
labels:
  - corrective
  - ci
  - architecture-tests
links:
  - INTK-065
refs:
  - docs/engineering.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
commits:
  - 0a6ccca799eb670825e60b614cf24b846cdf4572
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/708'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: 0a6ccca799eb670825e60b614cf24b846cdf4572
delivery_recorded_at: '2026-09-08T16:01:36.460Z'
archived: false
created: '2026-09-08T14:56:55.766Z'
updated: '2026-09-08T16:01:36.460Z'
---

## What

Investigate and correct the two architecture-test failures exposed by PR #706 current-head CI without absorbing them into the principal inventory diff.

## Evidence

Run https://github.com/collisionengineers/pegasus/actions/runs/34240260482, unit job 102108502034: Core 1907 passed / 14 skipped, then architecture 2 failed / 116. DependencyDirectionTests.IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary expects IInstructionExtractionPolicy directly in ProcessIntake; current ProcessIntake consumes InstructionExtractionPolicySelector. StagedArtifactReconciliationFunctionTests.FunctionDependsOnTheCanonicalStagedArtifactReconciler omits existing IImageIntakeCasePairing and ITriageCasePairing dependencies/callers. PR #706 changes neither the runtime targets nor these tests. Baseline source observation alone is not authority: verify current governing contracts before amending assertions.

## Scope

Two existing architecture-test files only after research and plan: preserve a meaningful Core-owned extraction-policy boundary through the existing selector and the existing exact Worker dependency/caller contract. No runtime, constructor, policy, new dependency, compatibility, infrastructure, alert or release-operation change; do not delete or weaken the architectural claims merely for green tests. INTK-002's adapter-fault/Web-composition scope is separate.

## Verification

Sole host verifier completed locked restore/build, both focused architecture tests, and full architecture project verification (116 tests), with no parallel host test/build activity. Original CI failures and changed-SHA evidence remain retained. Independent review and exact-merge proof passed. This is corrective-plan D2 CI reconciliation, not permission to expand the released feature scope.

## Outcome

PR #708 squash-merged into `dev` as
`0a6ccca799eb670825e60b614cf24b846cdf4572`. The author commit
`71c1bc1266583459d40b82c3d19d59af632afa7d` remains provenance; the merged
SHA is the reachable integration record. Schema-2 proof records four
exact-merge PASS attempts: locked restore, zero-warning/error Release build,
both focused architecture tests, and the full 116-test architecture project.

The original two architecture failures and optional pre-merge CI failures
remain in research/review/scratch history and were not relabelled or erased.
Both pre-merge TRXs were retained outside the disposable worktree with matching
SHA-256 values; the exact-merge TRXs are likewise retained in
`artifacts/verification/deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572/`.
This is non-deployable architecture-test correction (`n/a`): no deployment,
release-candidate promotion, or main update occurred.
