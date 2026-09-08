---
id: DELIV-056
type: ticket
title: Align intake regression fixtures with definitive instruction evidence
status: done
area: delivery-repository
order: 0
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:29:14.702Z'
  implementing: '2026-09-08T13:34:43.025Z'
  review: '2026-09-08T16:09:58.194Z'
  verifying: '2026-09-08T16:33:51.708Z'
  done: '2026-09-08T16:50:49.801Z'
labels:
  - regression
  - test-fixtures
  - corrective
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/engineering.md
commits:
  - 71a2d27c8836a44b639762469ec950f1c8c82802
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/709'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: 71a2d27c8836a44b639762469ec950f1c8c82802
delivery_recorded_at: '2026-09-08T16:53:07.600Z'
archived: false
created: '2026-09-08T13:28:13.915Z'
updated: '2026-09-08T16:54:29.858Z'
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

- PR [#709](https://github.com/collisionengineers/pegasus/pull/709) was squash-merged into `dev` on 2026-09-08 as `71a2d27c8836a44b639762469ec950f1c8c82802`.
- Final schema-2 proof is PASS for the exact merge: locked restore, Release integration build, seven corrected methods and the separate integrated HeldLease caller. This is not a broad-suite PASS or a D5 candidate claim.
- Before cleanup, retained pre-merge TRXs were copied and SHA-256 verified under the ignored stable path `artifacts/verification/deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802/`: broad 269-case FAIL `deliv-056-focused-host-20260908.trx` (`01E4E965C1203ECEBE13AB50DCF0DDCF16EBAD2A416F581D60119E76BF411C7F`), exact-six FAIL `deliv-056-corrections-host-20260908-1528.trx` (`FC765D98532AABD76BCA6AE245E6BEFCBA53696BAC56217ADF5B2392C9A5D377`), exact-seven FAIL `deliv-056-final-seven-host-20260908-1551.trx` (`55A836D0EB42E98826A5991B83A98E0675879D4319BCEA22989B01AAD61BD799`), and two-Audit PASS `deliv-056-audit-version-host-20260908-1600.trx` (`A44267FEB86853AEBD7039F53D2231B10CD51C86211476075DAA7957830B69D7`). The two exact-merge TRXs were already retained there by verification.
- [[INTK-066]] remains the unchanged owner of the unresolved UploadConfirmation/browser manual-attach contract; this closeout neither changes it nor treats the historical broad run as passing.
- Test-fixture work only: integrated to `dev`; deployment is `n/a`.
