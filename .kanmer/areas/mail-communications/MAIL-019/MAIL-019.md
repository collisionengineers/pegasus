---
id: MAIL-019
type: ticket
title: >-
  Post-release smoke asserts inbox intake liveness (active subscription, recent
  poll)
status: backlog
area: mail-communications
assignee: ''
profile: chore
labels:
  - release
  - smoke
links: []
archived: false
created: '2026-08-27T10:06:22.829Z'
updated: '2026-08-27T10:06:22.829Z'
---

## Problem

Release 33/34 notes state the recovery timer was not proven; neither release detected that no Graph subscription existed and no poll ran.

## Required outcome

The release smoke reads (read-only) one `Active` row in `ApprovedMailboxSubscriptions` and an `ApprovedInboxPollStates.LastCompletedAtUtc` newer than the recovery interval, and fails the release verification otherwise.
