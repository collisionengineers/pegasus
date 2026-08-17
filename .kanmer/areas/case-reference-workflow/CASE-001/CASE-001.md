---
id: CASE-001
type: ticket
title: >-
  Show or drop the unread TempData["CaseDetailsStatus"] written by Create and
  Intake/Details
status: backlog
area: case-reference-workflow
assignee: ''
profile: chore
labels:
  - simplify
  - web
  - follow-up
links:
  - SIMPLI-011
docs_todo: true
archived: false
created: '2026-08-17T14:37:29.159Z'
updated: '2026-08-17T14:37:29.159Z'
---

## Why

`src/Pegasus.Web/Pages/Cases/Create.cshtml.cs:228,270,384` and `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs:137` write `TempData["CaseDetailsStatus"]` ("This item already has a case." and a created-case message) before redirecting to the case workspace, but the workspace (`Pages/Cases/Details.cshtml`) reads only `CaseStatus` / `CaseError` / `CaseRequestSecret`. The messages are never shown. Found while planning [[SIMPLI-011]] (open question 5); left out of that ticket because making a hidden message visible is a behaviour change outside its diff.

## Scope

Decide with the operator surface in mind: either the writers move to `CaseStatus` (the message then appears on the workspace after the redirect — check the wording reads well beside the workspace's other status banners) or the four writes are deleted as dead. One list per concept: no third TempData status key.

## How to verify

`rg CaseDetailsStatus src/` returns nothing; `CaseCreateWebTests` and the intake details tests green; if shown, one Web test asserts the banner after the redirect.

## Outcome
