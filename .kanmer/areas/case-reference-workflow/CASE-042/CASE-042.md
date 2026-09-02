---
id: CASE-042
type: ticket
title: 'Awaiting instruction: image-initiated cases as a Pre-case queue on Cases'
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - cases
  - queues
  - image-initiated
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
docs_todo: true
archived: false
created: '2026-09-02T20:31:38.909Z'
updated: '2026-09-02T20:33:53.279Z'
---

## What

A Pre-case tab "Awaiting instruction" on `/Cases` listing image-initiated cases in AwaitingInstruction with reference, registration, vehicle, received, image count and source, with a quick view offering Add to an existing case and Create Case.

## Why

D38. Mockup source: `Pegasus_UI_v2_src/src/13-cases.js` (`awaiting`).

## Approach

- Extend CASE-025's rail; rows from CASE-032's projections.

## Verification

- [ ] Tab count equals rows; rail count includes it.

## Outcome
