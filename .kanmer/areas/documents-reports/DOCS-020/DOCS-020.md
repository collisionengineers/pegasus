---
id: DOCS-020
type: ticket
title: Keep report snapshots consistent and invalidate changed report inputs
status: preparing
area: documents-reports
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-07T20:21:48.709Z'
labels: []
groups:
  - EPIC-014
links:
  - CASE-047
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-09-07T19:58:09.568Z'
updated: '2026-09-07T20:21:48.709Z'
---

## What

Repair PR675 report review findings: freeze report inputs from one guarded Case version; invalidate report generations on source-document and signatory changes using existing contracts; refuse stale delivery; use LondonCalendar for civil report dates and operator presentation.

## Acceptance

Focused existing persistence tests cover a concurrent source mutation, adding/removing source documents, changing eligibility/name/qualifications/signature and refusing stale delivery. A BST-midnight test proves report dates. No new framework or domain policy owner. Current operator permission includes implementation, merge and deployment; return an independently reviewable PR.

## Outcome
