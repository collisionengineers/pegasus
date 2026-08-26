---
id: MAIL-014
type: ticket
title: Reset mailbox poll state when a disabled mailbox is reactivated
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels:
  - mailbox
  - reactivation
  - graph
  - intake
  - review-finding
links:
  - MAIL-013
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md
deployment: not-deployed
archived: false
created: '2026-08-26T18:09:46.409Z'
updated: '2026-08-26T18:09:46.409Z'
---

## Problem

A disabled approved mailbox may have its address changed. On reactivation, `EfApprovedInboxPollStore` validates the old stored address before resetting the activation state, so the mailbox can fail both notification wakes and fallback polling permanently.

## Required outcome

Treat a changed activation timestamp or scope fingerprint as a fresh activation cycle and replace the stored mailbox address/state before mismatch validation. Preserve fail-closed checks within the same activation cycle.

## Verification

Cover disable → address change → re-enable for notification-triggered intake and recovery polling, and retain the same-cycle mismatch rejection.

Related implementation: [[MAIL-013]], PR #563 review finding.

## Outcome
