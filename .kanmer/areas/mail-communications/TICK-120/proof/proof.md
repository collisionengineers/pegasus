## Proof (VERIFY2, 2026-08-20) — written on merged main, production release 13 = 2325ed4a

- File presence at 2325ed4a: `CaseWorkScheduling.cs` seven-day rule, `RunDueChasers.cs`, `Tasks.cshtml.cs:134` `OnPostRecordManualChaseAsync`, `EmailEvidenceFunctions.cs` `DueWorkSweepFunction`, `platform.bicep:523` 5-minute schedule — all confirmed present.
- Live production data (2026-08-20, read-only SQL against `pegasus-prod-sql-252ow37gij/pegasus`): `CaseDueWork` has 2 rows, both `Scheduled`, both with a real `DueBy` and a `NextChaseAtUtc` exactly 7 days after the case entered `NotReady` — matching `CaseChaseSchedule.FirstChaseAt`.
- Worker: `DueWorkSweepFunction` is one of the confirmed-enabled Worker functions (prod-diagnostics §6); its own log line states no outbound communication is ever attempted, matching the ticket's explicit scope limit (staff-sent copyable text only; MAIL-19 automatic sending remains `Later/0.5.0`).

**Blocking chain resolved:** TICK-116 (archived → BUG-001, done) and TICK-112 (archived) both concerned proving a genuine QDOS production journey existed before this workflow could be activated against it. Production now has two real mailbox-origin QDOS cases (QDOS26001, QDOS26002) each with a live `CaseDueWork` row. The `blocked` label is removed.

**Residual (named, not fabricated):** neither production case has yet reached its first 7-day chase point, so `CaseDueChasers` and `CaseManualChases` are both empty in production — no chaser has fired and no staff manual chase has been recorded yet. This is expected given case age, not a defect; nothing further is required of this ticket.
