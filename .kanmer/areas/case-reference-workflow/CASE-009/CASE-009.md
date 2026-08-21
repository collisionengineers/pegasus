---
id: CASE-009
type: ticket
title: >-
  Rename Case Details Engineer Queries to Queries and remove manual query
  creation
status: preparing
area: case-reference-workflow
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-21T07:51:43.337Z'
labels:
  - ui
  - case-detail
  - queries
  - operator-reported
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-21T07:51:29.215Z'
updated: '2026-08-21T07:51:43.337Z'
---

## Why

The Case Details page currently labels the email-derived query section **Engineer Queries** and offers a **Raise a Query** action. Queries are attached automatically from linked Query emails, so the manual creation action is misleading and must not be available.

## Verify

- The section heading is **Queries**.
- No **Raise a Query** button or manual query-creation control is present.
- Existing linked Query emails continue to populate the section. 

## Outcome
