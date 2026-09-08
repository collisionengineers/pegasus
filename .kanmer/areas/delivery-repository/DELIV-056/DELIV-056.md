---
id: DELIV-056
type: ticket
title: Align intake regression fixtures with definitive instruction evidence
status: review
area: delivery-repository
order: 0
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:29:14.702Z'
  implementing: '2026-09-08T13:34:43.025Z'
  review: '2026-09-08T16:09:58.194Z'
taken_at: '2026-09-08T13:35:14.533Z'
branch: >-
  DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence
worktree: .worktrees/deliv-056
claim_expires_at: '2026-09-08T16:37:39.509Z'
claim_controller: codex-mcp-client
lease_id: 4d01dfe4-c699-4761-b0e9-aef9f082f519
lease_revision: 8
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-056'
lease_phase: running-command
lease_heartbeat_at: '2026-09-08T16:07:39.509Z'
labels:
  - regression
  - test-fixtures
  - corrective
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/engineering.md
commits:
  - 23ea02f310a790c9aa3224a10de40efacf5444eb
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/709'
archived: false
created: '2026-09-08T13:28:13.915Z'
updated: '2026-09-08T16:09:58.194Z'
---

## What

Repair the shared D2 intake/display fixture root cause from PR700 run34196369756: old QDOS route/body-token test inputs no longer establish a definitive instruction.

## Why

The operator-approved corrective release requires the exact failed scenarios to run past setup and retain their assertions. This bounded follow-up owns current fixture corrections, not historical INTK-060/INTK-047/CASE-032 worktrees or claims. [[ENG-029]] owns its two separate retained fixtures and [[INTK-065]] owns the source inventory.

## Approach

- Reuse supplied genuine instruction evidence and existing test helpers/fakes; update only affected fixtures/callers.
- One owner for CustodyOutboxIntegrationTests, InstructionDraftWebTests, ImageViewingWebTests, MailWorkspaceWebTests, QdosTriageIntegrationTests, TriageQueuesWebTests, ImageIntakeWebTests, MailboxIntakeIntegrationTests, MultiFormatIntakeWebTests, RecoveryTests, SendToAiIntegrationTests, UploadConfirmationWebTests and TestUiFocusedRenderTests.
- Align the stale pre-handoff SendToAI display assertion in the same file to current handoff/access requirements without opening actions early.
- Preserve allocation, identity, role, replay, lifecycle, accessibility and destination assertions. No product policy changes or full new Triage/Query/Audit implementation.

## Verification

- [ ] Sole host verifier runs the exact affected existing selections after any required sequential build; retain original failure inventory and exits.
- [ ] Any genuine newly exposed defect is reported, not bypassed by weakening assertions.
- [ ] Independent review and one draft PR to dev.

## Outcome
