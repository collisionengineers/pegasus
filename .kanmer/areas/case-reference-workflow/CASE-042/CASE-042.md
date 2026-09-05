---
id: CASE-042
type: ticket
title: 'Awaiting instruction: image-initiated cases as a Pre-case queue on Cases'
status: review
area: case-reference-workflow
assignee: wf-build/case-042
profile: feature
stageEntered:
  preparing: '2026-09-02T22:20:38.061Z'
  review: '2026-09-04T19:11:02.503Z'
taken_at: '2026-09-04T17:58:44.553Z'
branch: task/case-042-awaiting-instruction-queue
worktree: .worktrees/case-042
claim_expires_at: '2026-09-04T18:28:44.553Z'
claim_controller: wf-build/case-042
lease_id: 706d76d5-1050-418e-bacd-148095033418
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-042'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T17:58:44.553Z'
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
  - CASE-045
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - 075daec19
  - c6d63ac89
  - 53641939f
  - a6f91dc94
  - b9fefcb92
  - 110b1b5f0
  - 353f3da1b
  - 60c80769f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/663'
archived: false
created: '2026-09-02T20:31:38.909Z'
updated: '2026-09-05T05:07:47.320Z'
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
