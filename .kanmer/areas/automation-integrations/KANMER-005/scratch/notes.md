2026-08-28 CI red on #593 `sql-integration (1)`: four `VehicleWorkflowTerminalTests` threw `CaseEditLeaseConflictException`. Cause: the fixture seeds the lease by raw SQL `UPDATE CaseWorkflows … EditLeaseHolder = {Staff.SubjectId} …` without `EditLeaseHolderKind`, so the retained kind was null and the new rule treated the holder as nobody's before the registration checks ran. Staff-vs-staff matching is correct. Fix: fixture retains `EditLeaseHolderKind = 'Staff'` (both UPDATEs); only raw-SQL lease seed in the test tree. Ran `dotnet test ./tests/Pegasus.IntegrationTests --configuration Release --filter "FullyQualifiedName~VehicleWorkflowTerminalTests"` locally: 9/9 passed, exit 0 (the one permitted test run, CI-red exception). Still waiting on the coordinator's "#592 merged" note before merging origin/dev.

## 2026-08-29 — Audited under the strict rule 14 (D20/D21) and KEPT in Done

This ticket was in the 2026-08-29 strict rule-14 sweep of EPIC-011's Done column
(D20/D21, `.kanmer/groups/EPIC-011/decisions-2026-08-29-done-rule.md`) and the
adjudication result was **KEEP**: it stays in Done, and no proof amendment is
required.

Reason: KANMER-005 is a concurrency *fix* to lease ownership, not a new named
capability behind a control or a gate. What it changed — atomic lease ownership at
claim and write boundaries, rejection of a competing claim while an unexpired lease
exists, holder-only edit/renew/release — sits on the existing lease path that staff
and Automation Actors already reach in production. There is no registered-but-
unreachable port, no permanently inert control and no closed composition gate in its
diff, so neither the D20 strict reading nor the D21 disabled/gated rule bites.

Recording an honesty note: unlike the other ten tickets in the sweep, KANMER-005's
individual adjudication record is absent from the workflow journal
(`wf_97d5ada4-51c`), which emitted ten `result` lines covering the seven reversals
and the other three keeps. The KEEP disposition here comes from the orchestrator's
adjudication summary, not from a per-ticket ruling document. If a later reader needs
the file-level census behind this keep, it has not been written down and would need
re-running.
