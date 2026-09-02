---
id: INTK-057
type: ticket
title: Do not emit case_created when the mail classification carries no case type
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - intake-allocation
  - fail-closed
  - low
groups:
  - EPIC-011
links:
  - MAIL-035
  - INTK-056
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-09-02T14:57:21.701Z'
updated: '2026-09-02T14:57:21.701Z'
---

## What

In `ProcessIntake`, downgrade `IntakeDecision.CaseCreated` to `NeedsSorting` (carrying the classification reason) when `MailClassificationDecision.CaseType` is null, so the item is registered in Unidentified instead of surfacing only as a failed row in `IntakeAllocationAttempts`, and `IntakeAllocation` stops raising `InvalidOperationException("The persisted allocation command has no accepted case type.")` into Application Insights.

## Why

Production, 2026-09-02 14:05:18Z, receipt `c45c65a8…` (QDOS EREF24, a bodyshop supplementary plus repair invoice on the wiped estate): the mail classification was `unclassified` (no ENGINEER NOTIFICATION or AUDIT REPORT NOTIFICATION title), the mail route was accepted, and the extraction policy still returned `case_created` — "A definitive instruction was identified and is eligible for case allocation." Allocation then failed with `case_type_unavailable` / `manual_review` and one `AppException`. The fail-closed outcome is right; the shape is not: the receipt is neither a case nor an Unidentified item, so the operator reads it as an email that never appeared. Found during the [[MAIL-035]] diagnosis.

## Approach

- Apply the downgrade in the same place `ProcessIntake` already downgrades a standalone Audit without a report ([[INTK-056]] touches the neighbouring rule), reusing `IntakeDecisionPolicy`; no new decision code.
- Keep `IntakeAllocationFailureKind.CaseTypeUnavailable` as the defensive last line, but it should no longer be reachable from the mailbox path.
- Core test: accepted route + `unclassified` classification + definitive-looking content → `NeedsSorting` with the classification reason, registered in Unidentified.

## Verification

- [ ] The EREF24 shape produces an Unidentified item and no `IntakeAllocationAttempts` failure.
- [ ] No `InvalidOperationException` from `IntakeAllocation` in App Insights for that shape.
- [ ] Canonical restore/build/test pass.
