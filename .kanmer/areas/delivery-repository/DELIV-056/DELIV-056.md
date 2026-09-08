---
id: DELIV-056
type: ticket
title: Align intake regression fixtures with definitive instruction evidence
status: preparing
area: delivery-repository
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-08T13:29:14.702Z'
labels:
  - regression
  - test-fixtures
  - corrective
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/engineering.md
archived: false
created: '2026-09-08T13:28:13.915Z'
updated: '2026-09-08T13:29:14.702Z'
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
