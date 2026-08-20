---
id: INTK-017
type: ticket
title: >-
  Deterministic extraction rules populate the full case detail set from
  instruction documents
status: verifying
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T03:50:41.880Z'
  review: '2026-08-20T04:01:10.225Z'
  verifying: '2026-08-20T04:07:03.983Z'
taken_at: '2026-08-20T03:44:15.187Z'
branch: task/intk-017-extraction-coverage
worktree: ../pegasus-worktrees/intk-017
labels:
  - extraction
  - operator-reported
  - QDO26002
links:
  - ENG-004
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
commits:
  - da86502f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/443'
archived: false
created: '2026-08-20T03:16:37.667Z'
updated: '2026-08-20T04:07:03.983Z'
---

## What

Operator, 2026-08-20, verbatim: *"See case QDO26002 - majority of details are not being extracted. We need deterministic rules for this to extract all the relevant case details."*

Build out the deterministic extraction rules so a real instruction document (QDO26002's is the reference sample) populates the relevant case details — parties, vehicle, contacts, addresses, dates, references — instead of leaving the majority empty.

## Why

The extraction pipeline exists but its rule coverage is thin; QDO26002 in production demonstrates the gap concretely.

## Verification

- [ ] Re-running extraction over QDO26002's instruction document populates the majority of its case details correctly (list the exact fields in the plan).
- [ ] Rules are deterministic and covered by fixture tests using the document's real text shapes.
- [ ] Wrong-value suggestions do not regress (see the vehicle-details defect ticket).
