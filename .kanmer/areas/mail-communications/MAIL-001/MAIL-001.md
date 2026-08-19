---
id: MAIL-001
type: ticket
title: Keep known mail classifications out of the generic Other destination
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels:
  - pr-review
  - TICK-044
  - PR-411
groups:
  - EPIC-006
links: []
blocks:
  - TICK-044
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-19T08:38:20.492Z'
updated: '2026-08-19T08:38:30.609Z'
---

## What

Correct PR #411 so every known classification retains a distinct operational view instead of mapping to `MailOperationalDestination.Other`. Reserve `Other` for the reasoned novel-classification escape hatch.

## Why

The operator's 2026-08-19 decision, recorded in [[TICK-057]], explicitly says known classifications must not collapse into a generic Other queue. PR #411 currently maps General, non-query Billing, Non-client-related, In-progress, non-Triage Pre-instruction, Internal CC, all Sent families, and reasoned Other to the same `Other` destination; FRD-08's added table says the same.

## Acceptance

- [ ] FRD-08 and the Core policy distinguish every known category/subtype operationally; `Other` is used only for a reasoned novel classification.
- [ ] `Ambiguous` and `Unclassified` still fail closed to Needs sorting.
- [ ] `pre-instruction-emails/triage-request` remains the only classification mapped to Triage; this does not change Triage workflow semantics.
- [ ] Tests assert the operator decision without duplicating a contradictory generic-Other mapping.
- [ ] The PR report, capabilities entry, and current architecture match the corrected behavior.

## Review source

Blocking finding from independent review of [[TICK-044]] / PR #411.
