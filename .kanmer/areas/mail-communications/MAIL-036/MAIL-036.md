---
id: MAIL-036
type: ticket
title: Preserve the wipe-time email cutoff across Graph replay
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-014
links:
  - MAIL-031
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-09-07T20:01:20.357Z'
updated: '2026-09-07T20:01:20.357Z'
---

## What

Preserve an explicit processing cutoff when Pegasus intake/mail test data is wiped. Old emails must stay excluded when delta tokens reset, queued notifications arrive late or retained identities have been removed. Preserve mailbox onboarding/configuration and reuse its start boundary where appropriate; one effective oldest admissible receive time applies to both polling and direct notification intake.

## Acceptance

Existing wipe workflow records a current cutoff; focused old/new-message tests after wipe and delta reset show old mail excluded and newly received/forwarded mail accepted. No live wipe is implied by implementing this change. The operator's current request supersedes old replay-from-onboarding behavior.

## Outcome
