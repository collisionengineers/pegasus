---
id: DELIV-058
type: ticket
title: >-
  Align architecture assertions with current extraction selection and pairing
  composition
status: review
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T14:58:28.071Z'
  review: '2026-09-08T15:20:07.226Z'
taken_at: '2026-09-08T15:09:15.962Z'
branch: DELIV-058-architecture-assertions
worktree: .worktrees/deliv-058
claim_expires_at: '2026-09-08T15:48:10.819Z'
claim_controller: codex-mcp-client
lease_id: 238978fe-9448-4810-8709-bb4a4db3b019
lease_revision: 2
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-058'
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T15:18:10.819Z'
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
  - 71c1bc1266583459d40b82c3d19d59af632afa7d
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/708'
deployment: n/a
archived: false
created: '2026-09-08T14:56:55.766Z'
updated: '2026-09-08T15:20:07.226Z'
---

## What

Investigate and correct the two architecture-test failures exposed by PR #706 current-head CI without absorbing them into the principal inventory diff.

## Evidence

Run https://github.com/collisionengineers/pegasus/actions/runs/34240260482, unit job 102108502034: Core 1907 passed / 14 skipped, then architecture 2 failed / 116. DependencyDirectionTests.IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary expects IInstructionExtractionPolicy directly in ProcessIntake; current ProcessIntake consumes InstructionExtractionPolicySelector. StagedArtifactReconciliationFunctionTests.FunctionDependsOnTheCanonicalStagedArtifactReconciler omits existing IImageIntakeCasePairing and ITriageCasePairing dependencies/callers. PR #706 changes neither the runtime targets nor these tests. Baseline source observation alone is not authority: verify current governing contracts before amending assertions.

## Scope

Two existing architecture-test files only after research and plan: preserve a meaningful Core-owned extraction-policy boundary through the existing selector and the existing exact Worker dependency/caller contract. No runtime, constructor, policy, new dependency, compatibility, infrastructure, alert or release-operation change; do not delete or weaken the architectural claims merely for green tests. INTK-002's adapter-fault/Web-composition scope is separate.

## Verification

Sole host verifier: affected locked restore/build then the two focused architecture tests and existing full architecture project (116 tests), with no parallel host test/build activity. Preserve the observed CI failures and truthful changed-SHA evidence. Independent review and exact-merge proof required. This is corrective-plan D2 CI reconciliation, not permission to expand the released feature scope.

## Outcome
