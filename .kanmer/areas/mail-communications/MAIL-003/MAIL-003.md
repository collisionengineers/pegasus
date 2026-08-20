---
id: MAIL-003
type: ticket
title: Fix PollSentEvidence rejecting the correctly approved mailbox
status: done
area: mail-communications
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-20T03:27:23.549Z'
  review: '2026-08-20T03:49:02.082Z'
  verifying: '2026-08-20T04:02:55.809Z'
  done: '2026-08-20T12:46:14.015Z'
labels:
  - defect
  - sent-evidence
  - production
  - worker
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
prs:
  - '439'
deployment: production
archived: false
created: '2026-08-20T03:25:25.713Z'
updated: '2026-08-20T12:46:18.941Z'
---

## What

`Pegasus.Core.Workflow.PollSentEvidence.ExecuteAsync` throws `UnauthorizedAccessException` ("The claimed mailbox is not approved for Sent-evidence polling.") on every run — 2,080 times in 48 h (schedule `15 * * * * *`) — even though the worker's `Graph__MailboxId` (6118dbe0-4c94-48aa-8361-b803d6c9d52d) and `Graph__SentFolderId` match the `ApprovedMailboxes` row for instructions@collisionengineers.co.uk exactly, with `AllowSentEvidence=True`, `State=Approved`.

Find why the approval check rejects an approved mailbox (identity normalisation? folder-identity comparison? route-scope mapping?) and fix it. Failing that check should also not throw an unhandled exception every minute — an unapproved mailbox is an expected state, handled without exception noise.

## Why

Sent-evidence polling (MAIL-14/16 dependency) is silently dead in production, and the exception storm pollutes telemetry (~half of all worker exceptions).

## Verification

- [ ] Regression test reproducing the rejection with the production-shaped mailbox row.
- [ ] After deploy: PollSentEvidence completes without exceptions; Sent-folder polling actually reads the folder.
