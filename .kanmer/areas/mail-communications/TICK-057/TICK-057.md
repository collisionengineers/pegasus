---
id: TICK-057
type: ticket
title: >-
  UI-14 — Detailed classified-email views with distinct Unidentified and Triage
  queues
status: done
area: mail-communications
order: 1820
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:48.884Z'
  review: '2026-08-20T22:46:54.024Z'
  verifying: '2026-08-20T23:26:02.156Z'
  done: '2026-08-21T15:10:09.883Z'
labels:
  - capability
  - UI-14
  - next
groups:
  - EPIC-003
  - EPIC-006
links:
  - TICK-009
  - TICK-010
blocks:
  - TICK-056
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 4b851ded
  - 4a13def9
prs:
  - '491'
deployment: production
archived: false
created: '2026-08-12T15:05:19.394Z'
updated: '2026-08-26T14:34:45.991Z'
---

## What

Implement UI-14: SQL-filtered retained-email views for Receiving work, Queries, named detailed classifications, reasoned Other, Unidentified and the distinct Triage workflow.

## Why

The current exact-message detail derives its operational destination from the canonical Core policy, but /Inbox cannot select that destination or a named detailed classification. Counts and pages must describe the selected current-classification view, not a post-filtered 25-row slice.

## Approach

- Reuse MailOperationalDestinationPolicy and the current persisted-classification projection.
- Apply the selected policy criterion or exact detailed category in SQL before count and pagination.
- Preserve mailbox, folder, search, queue and page through accessible list/detail navigation.
- Derive the destination on read; add no stored destination, taxonomy copy, generic filter framework or message action.

## Verification

- [x] Receiving work, Queries, reasoned Other, Unidentified and Triage are distinct, and a named detailed classification is selectable.
- [x] Unidentified replaces only the old broad Needs sorting wording; Triage remains separate.
- [x] Populated SQL tests prove current corrected classification, counts and paging.
- [x] Authenticated Web tests prove accessible active navigation and exact detail/return context.

## Notes

- Source: docs/capabilities.md — UI-14.
- Canonical owner: docs/frd/frd-08-email-mailbox-and-background-processing.md.
- Activated for local implementation by the operator; no live mailbox, deployment or external write is authorized.
