---
id: MAIL-017
type: ticket
title: >-
  Reactivate the identity-bound approved mailbox de-activated by the release-33
  migration
status: done
area: mail-communications
order: 2450
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-27T10:06:28.781Z'
  review: '2026-08-27T10:54:20.505Z'
  verifying: '2026-08-27T17:07:58.727Z'
  done: '2026-08-27T17:14:16.670Z'
labels:
  - mailbox
  - release-defect
  - migration
groups:
  - EPIC-010
links:
  - MAIL-015
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - bd34b1a0
  - 61d8053961bc8cf476e531d1e02468ee32f95961
prs:
  - '571'
archived: false
created: '2026-08-27T10:06:22.772Z'
updated: '2026-09-03T09:06:56.687Z'
---

## Problem

Migration `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions` adds nullable `ApprovedMailboxes.ActivatedAtUtc` and, because the EF seed omits it, emits `UpdateData(ActivatedAtUtc = null)` for the seeded mailbox `49f47eb9-…` — the live, identity-bound production mailbox. Every intake path filters on `ActivatedAtUtc != null` (`EfApprovedMailboxStore.ListPollableAsync/GetPollableAsync`, `EfApprovedMailboxSubscriptionStore.ListMaintenanceCandidatesAsync/GetActiveAsync`, `EfApprovedInboxPollStore` claim), so since release 33 no poll runs, no Graph subscription is created (`ApprovedMailboxSubscriptions` is empty in prod), and the Mail page reads stale. Verified read-only on 2026-08-27 against prod SQL and App Insights. The Graph-notification wiring itself is correct.

## Required outcome

A data-repair migration (raw SQL, no model change) sets `ActivatedAtUtc = SYSDATETIMEOFFSET()` where `State = 'Approved' AND ActivatedAtUtc IS NULL AND MailboxIdentity IS NOT NULL AND InboxFolderIdentity IS NOT NULL`. `docs/operations.md` records the defect and repair. Operator interim action: re-save the mailbox in Administration › Mailboxes, then send a fresh test e-mail (mail received before activation is skipped by design).

## Outcome

Shipped as planned in PR #571 (https://github.com/collisionengineers/pegasus/pull/571), merged into `dev` 2026-08-27T17:07:44Z at `61d8053961bc8cf476e531d1e02468ee32f95961`: migration `20260827100901_ReactivateBoundApprovedMailboxes`, migration-head test assertion, and the `docs/operations.md` release-33 note. Proof PASS at code level. The migration is not yet deployed — prod was already repaired by the operator re-save (2026-08-27 10:20:33Z), so the migration is expected to match zero prod rows; live evidence of it applying belongs to the next release's record. No follow-up tickets.
