## Backfill research (VERIFY2, 2026-08-20)

Written retrospectively — this capability was implemented and released before this ticket was worked; this records what was found, not what was built.

**Capability rows (docs/capabilities.md):** CASE-17 (due-by extraction and overdue display), CASE-18 (seven-calendar-day missing-information chase schedule), CASE-19 (hold/release preserving the chase interval), CASE-20 (general case tasks/reminders), MAIL-18 (generate copyable chaser messages for staff to send manually — explicitly NOT automatic sending; MAIL-19, automatic outbound, is `Later/0.5.0` and out of scope). Owner: frd-01 "Due work, chasing, and action history".

**Core policy owner:** `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs` — `CaseDueWork` record (`DueBy`, `State`, `NextChaseAtUtc`, `RemainingChaseInterval`), `ICaseDueWorkQueries`/`ICaseDueWorkStore`, and `CaseChaseSchedule` static policy: `FirstChaseAt`/`NextChaseAt` both add exactly 7 London-calendar days (`local.Date.AddDays(7).Add(local.TimeOfDay)`, lines 84-86 and 90-92), `RemainingInterval`/`ResumeAt` preserve the remaining interval across a hold/release cycle (CASE-19).

**Chase generation:** `src/Pegasus.Core/Tasks/RunDueChasers.cs` — `IDueWorkOccurrence`, `RunDueChasers` class (line 94), `GeneratedCount` in its result. Manual record: `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs` `IRecordManualCaseChase`/`ManualChaseRecord`.

**Staff-facing caller:** `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs:134` `OnPostRecordManualChaseAsync` — the copyable-chaser surface. Injected `IRecordManualCaseChase recordManualCaseChase` (line 21).

**Worker composition:** `src/Pegasus.Worker/EmailEvidenceFunctions.cs:49-72` — `DueWorkSweepFunction` (TimerTrigger `%DueWorkSweepSchedule%`) calls `RunDueChasers.ExecuteAsync(maximumItems: 50, ...)`. Its own log message states explicitly: *"No outbound communication was attempted and no sending, receipt, or delivery was claimed"* — matching the ticket's scope note that automatic outbound remains out of scope (MAIL-19).
Timer schedule: `infra/modules/platform.bicep:523` `DueWorkSweepSchedule = '0 */5 * * * *'` (every 5 minutes). `DueWorkSweepFunction` is one of the confirmed-enabled Worker functions (prod-diagnostics §6).

**File-presence at production (release 13 = 2325ed4a):** all of the above confirmed present via `git show 2325ed4a:<path>` — `Tasks.cshtml.cs:134` has `OnPostRecordManualChaseAsync`; `CaseWorkScheduling.cs` has the `AddDays(7)` rule at the same lines; `EmailEvidenceFunctions.cs` has `DueWorkSweepFunction`; `platform.bicep:523` has the 5-minute schedule.

**Live read-only SQL (2026-08-20, prod, AAD token, no writes):**
```sql
SELECT CaseId, DueBy, State, NextChaseAtUtc, HeldAtUtc, Version FROM CaseDueWork;
```
Result: 2 rows — `aa61d7b1…` (QDOS26001) `DueBy=2026-07-15, State=Scheduled, NextChaseAtUtc=2026-08-25T15:09:05Z`; `0b22b9d6…` (QDOS26002) `DueBy=2026-08-16, State=Scheduled, NextChaseAtUtc=2026-08-27T03:03:18Z`. Both `DueBy` are real, both `NextChaseAtUtc` are 7 days ahead of when each case entered `NotReady`, consistent with `CaseChaseSchedule.FirstChaseAt`.
```sql
SELECT COUNT(*) FROM CaseDueChasers;   -- 0
SELECT COUNT(*) FROM CaseManualChases; -- 0
```
Neither case has yet crossed its first 7-day chase point (both `NextChaseAtUtc` are in the future relative to 2026-08-20), so no chaser has fired yet and no staff manual chase has yet been recorded — expected given both cases' real ages, not a defect.

**Blocked-label investigation:** TICK-120 linked to [[TICK-116]] ("Prove one genuine QDOS mailbox-to-Case/PO production journey", archived, consolidated into [[BUG-001]]), which itself was blocked by [[TICK-112]] ("Establish the QDOS Organisation and Principal in production", archived — mechanically imported, no independently actionable scope). `BUG-001` is `done` (merged PRs #386, #394; "QDOS identity is now established... from the three exact recorded sender domains, or the one proved prior/original sender of a Collision Engineers staff forward"). Production now demonstrably has two real, mailbox-originated QDOS cases (QDOS26001, QDOS26002, both audit type, both mailbox-origin — prod-diagnostics §7) each with a live `CaseDueWork` row carrying a real `DueBy`. The blocking condition — no genuine QDOS production journey existed to activate the chase workflow against — is resolved by live production evidence. The `blocked` label was removed from TICK-120 as part of this verification pass.
