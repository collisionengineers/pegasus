---
id: MAIL-017
type: ticket
title: >-
  Reactivate the identity-bound approved mailbox de-activated by the release-33
  migration
status: preparing
area: mail-communications
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-27T10:06:28.781Z'
labels:
  - mailbox
  - release-defect
  - migration
links:
  - MAIL-015
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-27T10:06:22.772Z'
updated: '2026-08-27T10:06:28.781Z'
---

## Problem

Migration `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions` adds nullable `ApprovedMailboxes.ActivatedAtUtc` and, because the EF seed omits it, emits `UpdateData(ActivatedAtUtc = null)` for the seeded mailbox `49f47eb9-…` — the live, identity-bound production mailbox. Every intake path filters on `ActivatedAtUtc != null` (`EfApprovedMailboxStore.ListPollableAsync/GetPollableAsync`, `EfApprovedMailboxSubscriptionStore.ListMaintenanceCandidatesAsync/GetActiveAsync`, `EfApprovedInboxPollStore` claim), so since release 33 no poll runs, no Graph subscription is created (`ApprovedMailboxSubscriptions` is empty in prod), and the Mail page reads stale. Verified read-only on 2026-08-27 against prod SQL and App Insights. The Graph-notification wiring itself is correct.

## Required outcome

A data-repair migration (raw SQL, no model change) sets `ActivatedAtUtc = SYSDATETIMEOFFSET()` where `State = 'Approved' AND ActivatedAtUtc IS NULL AND MailboxIdentity IS NOT NULL AND InboxFolderIdentity IS NOT NULL`. `docs/operations.md` records the defect and repair. Operator interim action: re-save the mailbox in Administration › Mailboxes, then send a fresh test e-mail (mail received before activation is skipped by design).
