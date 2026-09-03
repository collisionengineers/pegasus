---
id: INTK-037
type: ticket
title: Replace raw Triage identifiers with business-readable identities
status: backlog
area: intake-processing
order: 470
assignee: ''
profile: fix
labels:
  - ui
  - design
  - triage
  - follow-up
links:
  - PLAT-015
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-25T06:39:54.660Z'
updated: '2026-09-03T15:15:27.864Z'
---

## What

Replace raw technical identities on Triage details with the business references and selectors an operator can act on.

## Why

The copy audit in [[PLAT-015]] found linked-case GUIDs, a typed Case ID GUID input, finding and evidence GUIDs, and an Internet message identity exposed in the reply picker. These values are storage or transport identities, not operator language.

## Requirements

- Show a linked case by its case reference and other accepted identifying values, not its GUID.
- Select a case through the existing case search or selector; do not require typed GUID entry.
- Present findings and evidence through their accepted labels and context rather than raw IDs.
- Present a reply source by mailbox, sender, subject, and time as available; keep Internet-message identity internal.
- Reuse existing Core queries and selectors rather than creating another identity mapping.

## Verification

- [ ] No Triage page displays or asks an operator to enter a GUID or Internet-message identity.
- [ ] Selection and display use existing business-readable case, evidence, and retained-mail conventions.
- [ ] Authorization and the underlying immutable identities are unchanged.

## Outcome
