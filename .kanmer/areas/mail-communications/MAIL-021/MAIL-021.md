---
id: MAIL-021
type: ticket
title: >-
  Correct the StaleAfter rationale in RetainedMail for the five-minute recovery
  schedule
status: backlog
area: mail-communications
assignee: ''
profile: chore
labels:
  - docs
  - mailbox
links: []
archived: false
created: '2026-08-27T10:06:22.871Z'
updated: '2026-08-27T10:06:22.871Z'
---

## Problem

`src/Pegasus.Core/Intake/RetainedMail.cs` (StaleAfter comment) reasons from a one-minute inbound poll; recovery now runs every five minutes with notifications as the primary path.

## Required outcome

Comment states the current schedule and the meaning of the 15-minute threshold under it; no behaviour change.
