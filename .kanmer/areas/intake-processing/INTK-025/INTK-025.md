---
id: INTK-025
type: ticket
title: >-
  Extract report-sourced vehicle details and accident circumstances as QDOS
  policy rules
status: implementing
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-21T12:31:17.629Z'
taken_at: '2026-08-21T12:30:28.469Z'
branch: task/intk-025-qdos-report-rules
worktree: ../pegasus-worktrees/intk-025
labels:
  - extraction
  - corpus
  - qdos
links:
  - INTK-023
  - INTK-024
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-21T12:28:23.300Z'
updated: '2026-08-21T12:31:17.629Z'
---

## What

The approved mapping's rules 5 and 7 ([[INTK-024]]), under the operator's
constraint (2026-08-21): **these labels and rules are QDOS-specific** — they
live in `QdosInstructionExtractionPolicy`, never as provider-neutral engine
behaviour.

1. **Report-sourced details (rule 5):** inside report-titled documents only,
   the bodyshop grammar supplies fallback candidates — the `Vehicle:` line as
   a vehicle description (make/model), `Reg No`, and the labelled mileage
   row. The pre-incident-value guide mileage ("…at 82500 Miles") never
   qualifies. The instruction letter still outranks the report.
2. **Accident circumstances (rule 7):** the paragraph following the letter's
   "…following accident circumstances?" line becomes the Accident
   circumstances candidate (the field is currently never populated).
3. **Relocation:** the `TP `-prefix guard landed in the engine's label
   regexes ([[INTK-023]]) encodes QDOS letter grammar — it moves to
   policy-supplied configuration so the engine stays neutral.

## Verification

- [ ] The mapping-corpus emails whose letters lack a description but whose
      reports carry `Vehicle:` land make/model; guide mileage never becomes
      the odometer (corpus facts).
- [ ] Accident circumstances populate for the letter shapes carrying the
      prompt line; absent prompt → field stays empty.
- [ ] Engine source contains no QDOS grammar (no `TP` literal); QDOS unit
      facts cover the guard through policy configuration.
- [ ] `QdosMappingExtractionTests` extended; policy version bumped; suites
      green; Release build 0/0.
