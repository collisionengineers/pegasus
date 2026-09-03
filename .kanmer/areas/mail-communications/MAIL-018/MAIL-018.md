---
id: MAIL-018
type: ticket
title: Surface mailbox activation and Graph subscription health on the Mailboxes page
status: done
area: mail-communications
order: 2460
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-27T14:27:15.604Z'
  review: '2026-08-27T18:07:02.709Z'
  verifying: '2026-08-27T18:39:21.310Z'
  done: '2026-08-27T18:45:53.248Z'
labels:
  - mailbox
  - observability
groups:
  - EPIC-010
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 77989d47
  - 47ebad54
  - 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/577'
archived: false
created: '2026-08-27T10:06:22.803Z'
updated: '2026-09-03T09:06:56.762Z'
---

## Problem

An Approved-but-unactivated mailbox, or a dead/unrenewed Graph subscription, is invisible to the operator: the Mail freshness banner reads poll state only and the Mailboxes page shows `LastCompletedAtUtc` only. The release-33 de-activation defect went unnoticed for a day.

## Required outcome

Mailboxes page shows `ActivatedAtUtc` and `ApprovedMailboxSubscriptions.LifecycleState` / `ExpiresAtUtc` / `LastMaintenanceFailureCode` per mailbox, within the design authority's no-explanatory-copy rules.

## Outcome

Delivered by PR #577 (https://github.com/collisionengineers/pegasus/pull/577), merged into `dev` on 2026-08-27 at 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f; proof PASS at that SHA. The Mailboxes page now carries Activated and Subscription columns (labels and values only). The change is on `dev` only and not yet deployed; the live `/Administration/Mailboxes` screenshot proof belongs to the next release's evidence. Shipped differently than planned: only the Mailboxes Test UI snapshot was regenerated; 49 unrelated snapshot regenerations were deferred to [[MAIL-023]].
