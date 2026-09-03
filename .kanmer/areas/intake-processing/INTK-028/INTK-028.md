---
id: INTK-028
type: ticket
title: Read report mileage from a multi-column Speedo line
status: done
area: intake-processing
order: 1390
assignee: ''
profile: fix
stageEntered:
  implementing: '2026-08-21T21:37:38.666Z'
  review: '2026-08-21T22:06:37.708Z'
  verifying: '2026-08-22T03:43:36.615Z'
  done: '2026-08-22T03:43:42.951Z'
labels:
  - regression
  - qdos26008
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T18:17:17.436Z'
updated: '2026-09-03T09:06:49.606Z'
---

## Why

QDOS26008's bodyshop report carries a `Speedo:` value and the operator confirmed it is present, yet no mileage was extracted.

**Root cause.** `QdosInstructionExtractionPolicy.ReportFacts` anchors the speedo rule to line start (`^speedo\s*:`) and captures the whole line remainder (`.*\d.*$`). Real report lines are multi-column — `Vehicle: … Colour: … Speedo: … Reg No: …` — which the *neighbouring* Vehicle rule proves, because it explicitly cuts its own value at `colour|speedo|reg no|reg`. So the label is never matched mid-line; and where it is matched the value carries the following columns and fails `InstructionFieldEngine.ParseMileage`.

A second exposure: `IsReportFragment` recognises a report only by the word "report" in the retained file name, so a report named anything else never gets the report grammar at all.

## How to verify

Replay QDOS26008's actual report through the extraction tests — mileage must appear with the report as its cited source. Full corpus regression to prove no other provider's extraction shifted.
