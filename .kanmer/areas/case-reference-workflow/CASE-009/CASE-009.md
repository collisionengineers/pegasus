---
id: CASE-009
type: ticket
title: >-
  Show auto-attached Query emails on Case Details and remove manual query
  creation
status: preparing
area: case-reference-workflow
order: 30
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-21T07:51:43.337Z'
labels:
  - ui
  - case-detail
  - queries
  - operator-reported
  - mail-association
links:
  - CASE-027
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-21T07:51:29.215Z'
updated: '2026-09-02T01:10:30.568Z'
---

## Why

The Case Details page must call this section **Queries**, not **Engineer Queries**. It must not offer a **Raise a Query** action: query correspondence is sourced from emails already linked to the Case and classified as a Query.

## Verify

- The heading is **Queries**.
- The Case Details page renders a read-only list of emails linked to that Case whose classification is Query.
- The panel has a truthful empty state when no qualifying linked email exists.
- No **Raise a Query** button or manual query-creation control is present.
- The implementation does not create, reply to, resolve, or manually associate queries, and does not mutate any mailbox.

## Outcome
