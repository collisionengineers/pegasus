---
id: INTK-044
type: ticket
title: >-
  Automatic Audit allocation failed 'unexpected/blocked' for EREF10 (receipt
  f2ac0509) and staff have no recovery route
status: done
area: intake-processing
order: 2360
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-27T11:32:56.484Z'
  review: '2026-08-27T14:24:44.079Z'
  verifying: '2026-08-27T16:56:32.758Z'
  done: '2026-08-27T17:07:56.969Z'
labels:
  - audit
  - allocation
  - release-defect
groups:
  - EPIC-010
links:
  - MAIL-017
  - MAIL-020
  - INTK-045
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - 2112057d
  - f1bb150a
  - 935d58ff2e5f505620672e211bf420a9df71b295
prs:
  - '#572'
archived: false
created: '2026-08-27T10:41:44.794Z'
updated: '2026-09-01T14:44:33.946Z'
---

## Problem

2026-08-27 10:25Z, first live mail after the MAIL-017 reactivation: two QDOS forwards arrived together. EREF8 (`inspection_and_audit`) became QDOS26024. EREF10 (`Fw: (EREF10) RTA on 21/08/2026 : Ms Anthonia Ebosie (Our Ref: JF/ND/47907/1)`, receipt `f2ac0509-5de5-4555-93a2-399f4fea7587`) was routed, classified `audit`, had automatic standalone-audit evidence recorded (`3a45b161…`, asset `bcc54189…` = `1_Bodyshopreport513018-V1.pdf`, assessment repairable, all acceptance-store validity checks pass on the stored row), then `IntakeAllocationAttempts f598ac23…` failed at 10:25:29 with `FailureKind=unexpected`, `RecoveryDisposition=blocked`, no case, sequence not consumed. The attempt ran one second after the EREF8 acceptance, both under `Serializable` transactions.

The exception text was only logged (`EfIntakeAllocationStore.LogAllocationFailure`, event 4721) and Worker App Insights ingestion has been dead since ~10:12Z (MAIL-020), so the cause is not yet known. Verified not the cause: evidence row, original-report asset, receipt version (1 == expected), principal active, worker grants on every table the acceptance path writes, column lengths, unique/check constraints.

Compounding defect: `IntakeAllocationState.CanRetry` is false for `blocked`, and `Cases/Create` refuses `CaseType.Audit` ("created automatically"), so the receipt is a dead end — staff cannot retry, re-run, or create the Audit manually.

## Required outcome

1. Root cause of the failed acceptance found (with the exception captured once MAIL-020 restores telemetry, or by replaying the retained `.eml` locally) and fixed.
2. An `unexpected` automatic-Audit failure is recoverable by staff — retry from the Intake page or a fail-closed hand-off to Triage/Unidentified — never a terminal `blocked` with no route.
3. EREF10 exists as an Audit case.

## Outcome

PR #572 (https://github.com/collisionengineers/pegasus/pull/572) merged into `dev` 2026-08-27 at `935d58ff`. Root cause was a wrapped `SqlException 1205` deadlock between the concurrent EREF8/EREF10 acceptances; the retry loop now unwraps it and `BeginAsync` runs read-committed. Unclassified automatic failures are `Unexpected`/`ReloadThenRetry`, so staff can retry from Intake Details. EREF10 exists as `a.QDOS26025` (created live 10:51Z); its historical `blocked` attempt is left unchanged by design. Fix is on `dev` only, not yet deployed — production proof rides the next release. Follow-up: [[INTK-045]].
