---
id: CASE-030
type: ticket
title: 'Case: Report-sent evidence confirmation and Return to Engineer actions'
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - case
  - workflow
groups:
  - EPIC-011
links: []
blocks:
  - CASE-012
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-28T08:35:24.192Z'
updated: '2026-08-29T13:10:37.796Z'
---

## What

Wave 4 of [[EPIC-011]]. Action-bar "Report sent" opens a dialog listing detected/available retained Sent evidence for the Case and confirms the link (`LinkReportEvidence`; enters post-report work, never closes — D10); "Return to Engineer" on a Complete case = `ReopenCase(ReportPreparation)` with reason; "Close Case" keeps the reasoned closure outcomes.

## Owns

`src/Pegasus.Web/Pages/Cases/Closure.*`, `Tasks.*` (link evidence handler markup), `Cases/Shared/_CaseWorkflow.cshtml` (those two dialogs), tests.

## Blocked by

[[CASE-012]], the outbound-mail ticket (detection).
