---
id: MAIL-018
type: ticket
title: Surface mailbox activation and Graph subscription health on the Mailboxes page
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - mailbox
  - observability
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-27T10:06:22.803Z'
updated: '2026-08-27T10:06:22.803Z'
---

## Problem

An Approved-but-unactivated mailbox, or a dead/unrenewed Graph subscription, is invisible to the operator: the Mail freshness banner reads poll state only and the Mailboxes page shows `LastCompletedAtUtc` only. The release-33 de-activation defect went unnoticed for a day.

## Required outcome

Mailboxes page shows `ActivatedAtUtc` and `ApprovedMailboxSubscriptions.LifecycleState` / `ExpiresAtUtc` / `LastMaintenanceFailureCode` per mailbox, within the design authority's no-explanatory-copy rules.
