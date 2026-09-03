---
id: CASE-042
type: ticket
title: 'Awaiting instruction: image-initiated cases as a Pre-case queue on Cases'
status: preparing
area: case-reference-workflow
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T22:20:38.061Z'
labels:
  - cases
  - queues
  - image-initiated
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links:
  - CASE-044
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-09-02T20:31:38.909Z'
updated: '2026-09-03T10:54:49.132Z'
---

## What

A Pre-case tab "Awaiting instruction" on `/Cases` listing image-initiated cases in AwaitingInstruction with reference, registration, vehicle, received, image count and source, with a quick view offering **Add to an existing case** only.

Create Case was dropped on 2026-09-03 by operator answer: there is no lawful route (`IntakeDecisionPolicy.CanBecomeCase` is false for image-initiated receipts) and FRD-02 says image-only material merges into an eligible instructed Case. Nothing is drawn inert. The reverse direction — an instructed case pulling image material in — is [[CASE-044]], not this ticket.

## Why

D38. Mockup source: `Pegasus_UI_v2_src/src/13-cases.js` (`awaiting`).

## Approach

- Extend CASE-025's rail; rows from CASE-032's projections.

## Verification

- [ ] Tab count equals rows; rail count includes it.
- [ ] The quick view offers Add to an existing case and nothing else; no Create Case control exists, inert or otherwise.

## Outcome
