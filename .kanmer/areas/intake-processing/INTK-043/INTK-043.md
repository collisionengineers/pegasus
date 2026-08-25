---
id: INTK-043
type: ticket
title: Reduce ordinary intake source-reading latency to the ten-second p95 budget
status: preparing
area: intake-processing
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-25T15:26:55.430Z'
labels: []
groups:
  - EPIC-002
links:
  - AUTO-008
blocks:
  - DELIV-021
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-25T15:18:40.610Z'
updated: '2026-08-25T15:26:55.430Z'
---

## What
Measure and remove the ordinary QDOS e-mail/manual-upload source-reading delay so identification, classification, extraction, and case creation meet the agreed ten-second p95 budget.

## Why
Observed intake spent about seventeen seconds between staging and post-reader processing, while later classification and case creation took roughly one second. Polling changes cannot fix this section.

## Acceptance
- Add per-stage traces and an evidence-backed latency baseline.
- Remove verified avoidable source-reader work without introducing a second intake implementation.
- Ordinary QDOS e-mail and manual upload reach the truthful completed/case state within ten seconds p95; large inputs remain truthfully Processing.

## Outcome
