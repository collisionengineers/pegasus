---
id: MAIL-009
type: ticket
title: >-
  Resolve the effective sender at retention so the inbox never shows the desk
  address
status: done
area: mail-communications
order: 1490
assignee: ''
profile: fix
stageEntered:
  implementing: '2026-08-21T21:37:48.999Z'
  review: '2026-08-21T22:06:55.555Z'
  verifying: '2026-08-22T03:44:01.984Z'
  done: '2026-08-22T03:44:08.227Z'
labels:
  - regression
  - qdos26008
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T18:17:18.571Z'
updated: '2026-09-03T09:06:50.220Z'
---

## Why

The inbox showed QDOS26008's message as being from the desk, then corrected itself a moment later. The operator reports this keeps regressing across deploys.

**Root cause — and it is not the unwrap logic.** `EffectiveSenderAddress` is read off `MailRouteDecision`, which is written by **intake processing**, a later worker hop. The retained message row is created at poll time carrying only the raw desk sender, so the list truthfully renders what it has until processing lands. It is a timing window, not a broken rule — which is why fixing the unwrap repeatedly has not stopped it recurring.

## Fix direction

The staff-forward unwrap is a pure function of the message headers and body and needs nothing from intake processing. Resolve it at **retention** time, in the same write that creates the retained row, through the existing route evaluation. Intake processing then confirms rather than first-writes it.

Guard rail: where the effective sender genuinely is not yet resolvable, show a neutral pending state — never the desk address dressed as the sender.

## How to verify

A fresh staff-forwarded email must never render the desk address at any point, including first paint.
