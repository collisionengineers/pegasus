---
id: INTK-056
type: ticket
title: >-
  Read the standalone Audit report outcome from its status field, not any
  literal in the document
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - extraction
  - audits
  - unidentified
  - u45
groups:
  - EPIC-011
links:
  - INTK-031
  - INTK-032
  - CASE-014
  - MAIL-035
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-09-02T14:57:02.151Z'
updated: '2026-09-02T14:57:02.151Z'
---

## What

Make `QdosMailClassificationPolicy.EvaluateStandaloneAuditReport` (`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs`) decide Repairable versus Total Loss from the report's own status statement, and stop treating a vehicle-history marker anywhere in the document as the report outcome.

## Why

Production, 2026-09-02 14:10Z: Unidentified **U45** (receipt `71bff04d…`, QDOS audit instruction for REB//48099/1) was registered with `NoUsableIdentification` — "A standalone Audit instruction requires one attached original report stating Repairable or Total loss." The email had everything the rule asks for: the generated `AUDIT REPORT NOTIFICATION` letter and one original report (`Bodyshopreport734240-V1.pdf`), both read with embedded text. The report's Vehicle Details table says `Status  Repairable` and `Legal Status  Roadworthy`, and its title is `Repairable Damage Assessment Report` (13 unnegated `repairable` matches). It also carries the history line `Previous Cat N Total Loss`. `HasRepairable == HasTotalLoss == true`, so the exactly-one-outcome filter excluded the report, the evaluation returned null, and `ProcessIntake` downgraded `CaseCreated` to `NeedsSorting`.

Operator direction (2026-09-02): this is one of many third-party engineer report formats; the reference shape has been added to the local corpus as `corpus/documentexamples/tpreportexample.pdf`. The issuer-keyed survey of these formats is [[INTK-031]]; the fallback for a format that cannot be read is [[INTK-032]]. This ticket is the bounded rule fix so a report that plainly states its status is not lost to Unidentified.

## Approach

- Prefer a status statement (`Status Repairable` / `Status Total Loss` in a Vehicle Details table, or a `… Damage Assessment Report` title) over the whole-document literal scan; keep the literal scan as the second reading.
- Treat `Previous …`, `Cat N`, `Cat S` and similar history context as non-outcome for the purpose of the exactly-one filter; keep the existing negation regexes.
- Bump the classification policy version; add a Core fixture with the U45 text shape (no corpus content committed); keep the abstain path for a report that states neither.
- U45 is resolved by hand; the fix does not re-run it.

## Verification

- [ ] A report with `Status Repairable` plus a `Previous Cat N Total Loss` history line classifies as Repairable.
- [ ] A report stating only `Total Loss` in its status classifies as Total Loss; one stating both in its status still abstains.
- [ ] Existing standalone-Audit fixtures keep their outcomes; canonical restore/build/test pass.
