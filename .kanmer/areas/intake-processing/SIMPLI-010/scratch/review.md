# Review — PR #387 (SIMPLI-010) — 2026-08-17

Reviewer: independent subagent (no session context) commissioned by claude-code; claude-code implemented and merges.

## Changes (reviewer's words)
Deletes the `draft_ready` read alias: `EfIntakeReceiptStore.DecisionCodes` + `ParseDecision` branch (single-code `CaseCreated` filter, `ToCode` total so equivalent), `EfOperationsStore` succeeded-set entry, `EfCaseAcceptanceStore` comment, `IntakeContracts` paragraph; 14 fixture seeds across 12 test files → `case_created`; three test renames; design README contract rows use the live "Ready for case allocation" label; current-architecture and CONTEXT.md sentences; dead `_StatusChip` arm. Unknown persisted codes still throw.

## Comments
- N1 [fix-in-PR → Kanmer docs] report said "13 files"; it is 12 fixture files / 14 sites (+2 rename-only); README line refs were stale → **fixed** in the report.
- N2 [note] Deferring the stale-`dispatched` sweep to [[INTK-003]] is sound: retry/recovery truth is solely the allocation projection; the `dispatched` gap is a liveness hole, not a second truth. Plan step list annotated to match → **done**.
- N3 [ticket] `EfOperationsStore.MapIntakeState` second copy of the code table, returns `Unknown` for `blocked_intake`/`image_intake_registered` → already in [[INTK-002]].
- N4 [note] `docs/design/system/src/components/StatusChip.tsx:29` still 'draft ready' — imported design source, out of scope.
- N5 [note] 7 of 12 fixture files run only in CI's full SqlServer lane; change is monotone-safe.
- N6 [note] pre-existing: design README label table vs `Intake/Details.cshtml.cs:364-366` ("Needs text extraction"/"Failed" vs "Document text required"/"Technical failure"); `current-architecture.md:237` "join actual Case link" vs `EfOperationsStore.cs:158` `CaseId: null`. Not introduced here — **filed** as a note on [[INTK-002]]? No: separate small docs/labels chore → filed as INTK-004 below if not already covered.

## Plan coverage
All 6 steps DONE (6b — CI + merge + verify — open by design at review time). Ticket-vs-plan: holds — every case-existence reader (`IntakeAllocation.cs:220`, `IntakeMcpTools.cs:200-207`, `Intake/Details.cshtml:14-16`, `Mail/Message.cshtml.cs:118-124`) keys on `CurrentCaseId`/`CaseId`; `CaseCreated` is eligibility only.

## Report accuracy
22 files / +33 / −62 confirmed; N1 corrected.

## Verdict
**PASS.** Merge on green CI; then `verifying`.
