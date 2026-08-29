## Plan

1. Read `src/Pegasus.Core/LondonCalendar.cs` in full and report its current
   surface (`StartOfDay`, `StartOfNextDay`, `DateAt`, `ToUtc`) before changing
   it — confirms it is the right existing owner, per "search before you
   build".
2. Move the office-day/office-week rule out of
   `OperationsSnapshot.OfficeBoundaries` into `LondonCalendar`:
   - keep the `Europe/London` zone id and its exact
     `TimeZoneNotFoundException` / `InvalidTimeZoneException` → UTC fallback
     (preserve semantics, do not swallow silently);
   - add `DayAndWeekBoundariesAt(DateTimeOffset)` for the Monday-start week
     boundary, reusing `StartOfDay`/`DateAt`.
3. Update `GetOperationsSnapshot.ExecuteAsync` to call
   `LondonCalendar.DayAndWeekBoundariesAt`; delete the private
   `OfficeTimeZoneId` constant and `OfficeBoundaries` method.
4. Add `LondonCalendar.ToUtcRange(DateOnly from, DateOnly toInclusive)` for
   PLAT-051's half-open UTC range from an operator-entered inclusive
   `From`/`To` pair. This is the one named caller; no Reports type is added
   here.
5. Extend `tests/Pegasus.Core.Tests/LondonCalendarTests.cs` (reuse — the
   existing calendar test file) with: a GMT date, a BST date, the BST-start
   and GMT-start transition days, the Monday week start, the half-open range,
   and the reversed-range rejection.
6. Build (`dotnet build ./Pegasus.slnx --configuration Release`) and run the
   focused filter covering `LondonCalendarTests` and the existing
   `DashboardBoundaryTests` (unmodified, still exercises
   `GetOperationsSnapshot` through the shared owner). Commit, push.

## Reuse named

- `LondonCalendar.StartOfDay`, `StartOfNextDay`, `DateAt`, `ToUtc` — the
  existing conversion primitives; no new calendar type.
- `tests/Pegasus.Core.Tests/LondonCalendarTests.cs` — the existing test file
  for the calendar helper, extended rather than duplicated.

## Acceptance

- `GetOperationsSnapshot` has no private timezone/boundary logic left.
- `LondonCalendar` is the single owner of the `Europe/London` id, the day
  boundary, the Monday week start, the UTC fallback, and the half-open range
  conversion PLAT-051 needs.
- Build and focused Core test filter both green.

## Review findings — dispositions (round 2), 2026-08-29

Source: adversarial verifier report on PLAT-054 (verdict `needs-work`). Every
finding was re-checked against a real command before being disposed. The
verifier was correct on all five; nothing is rejected.

### [major] `LondonCalendar.ToUtcRange` has no production caller — FIXED (removed)

Confirmed. `grep -rn "ToUtcRange" --include=*.cs .` returned 3 hits: the
definition plus two test call sites. PLAT-051 is the named caller, is Wave 4,
and has not started, so this was test-only code against rule 14 and the
no-abstraction-without-a-concrete-caller rail.

`ToUtcRange` and its two tests are deleted. The two removed assertions are not
assertions weakened to pass a suite: the method they exercised no longer
exists. PLAT-051 is not blocked — `EfCaseQueryStore.cs:79-80` and `:92` are the
existing production precedent for the same half-open range, built from
`StartOfDay`/`StartOfNextDay`, which do have callers.

### [major] "One conversion owner" overstated; 3 of 4 duplicates unreported — FIXED (record) + DEFERRED (code) to [[PLAT-060]]

Confirmed. `grep -rn "Europe/London" --include=*.cs .` finds four zone lookups
outside `LondonCalendar`, not one:

1. `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:80` (the only one reported)
2. `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:23`
3. `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs:22`
4. `src/Pegasus.Web/Presentation/OperatorLabels.cs:650` — a second,
   byte-equivalent copy of the missing-zone → UTC fallback, under a doc comment
   reading "The one conversion."

None is inside this ticket's `Owns` list, and the lane rule forbids touching a
neighbour's files, so none is folded in here. All four are now named in `files`
and in the post-implementation report, and are tracked as **[[PLAT-060]]**
(platform-operations, EPIC-011, backlog) with the call-site table, the missing
instant→local primitive, and the behaviour change adopting the fallback would
make at sites 2 and 3.

The plan's acceptance criterion "LondonCalendar is the single owner of the
`Europe/London` id … the UTC fallback" is therefore **not met repo-wide** and
is restated in the report as met for the dashboard path only. That criterion
was over-scoped for a ticket whose `Owns` list contains none of the four
callers.

Note: the natural fix for site 1 is a public instant→local primitive on
`LondonCalendar`. It is deliberately **not** added here — with every caller
outside `Owns` it would ship as exactly the same rule-14 defect as
`ToUtcRange`. PLAT-060 owns the callers and can add it wired.

### [major] DST deviation wider than disclosed; changed path untested — FIXED (both)

Confirmed, and reproduced independently against the real `Europe/London` zone
(old vs new algorithm, four instants). Both `DayStartUtc` **and** `WeekStartUtc`
move by an hour on the two transition Sundays, because the old code derived the
week start from the day start via `DateTimeOffset.AddDays`, which preserves the
offset. Round 1 called it a one-line day-boundary fix.

**Chosen: keep the fix, cover it with tests that fail on the old code.** The old
values were wrong (London midnight on 2026-10-25 is 2026-10-24T23:00Z, BST
holding until 02:00 local); reinstating them to honour the ticket's
"behaviour-preserving" phrase would ship a known defect. The report now states
the two-field change with a before/after table.

Coverage claim corrected — round 1 said the transition-day tests covered it;
they call `StartOfDay`/`StartOfNextDay`, not `DayAndWeekBoundariesAt`. Two new
tests assert on `DayAndWeekBoundariesAt` and on both fields:
`DayAndWeekBoundariesOnTheGmtTransitionSundayUseBstMidnights` and
`DayAndWeekBoundariesOnTheBstTransitionSundayUseGmtMidnights`. Proven to fail
against the old algorithm by temporarily reverting the method, rebuilding and
re-running the filter — `Failed: 2, Passed: 8` — then restoring the shipped
code. That same run confirmed the old algorithm passed
`DayAndWeekBoundariesStartTheWeekOnMonday` and all three
`DashboardBoundaryTests`, so those genuinely did not cover the change.

### [minor] Monday week-start rationale deleted with the old method — FIXED

Confirmed. Re-homed onto `LondonCalendar.DayAndWeekBoundariesAt` as a
`<summary>`/`<remarks>` pair carrying both halves of the deleted reason: the
week starts on Monday because that is the week the office works to, and
counting from a UTC midnight would move the boundary by an hour for half the
year and silently reassign work between days. (Internal API documentation, not
operator-facing copy.)

### Verification after remediation

- `dotnet build ./Pegasus.slnx --configuration Release` — 0 warnings, 0 errors.
  (One earlier run hit `MSB3027` from a stale reusable MSBuild node holding
  `Pegasus.Core.dll`; `dotnet build-server shutdown` cleared it. Not a compile
  error, recorded rather than dropped.)
- Focused filter `LondonCalendarTests|DashboardBoundaryTests` — Failed: 0,
  Passed: 10, Skipped: 0. Same total as round 1, different composition (−2
  `ToUtcRange`, +2 transition-Sunday).
- `Pegasus.Core.Tests` whole project — Failed: 0, Passed: 1126, Skipped: 0.
- `Pegasus.ArchitectureTests` — Failed: 0, Passed: 100, Skipped: 0.

## Review findings — dispositions (round 3), 2026-08-29

Source: adversarial verifier report on PLAT-054 (verdict `needs-work`). The
major and both minors were confirmed against the branch and fixed. The two
informational findings require no code change.

### [major] DST-naive day end remained in `GetOperationsSnapshot` — FIXED

Confirmed. `ComposeNeedsAttentionAsync` derived the end from a UTC instant with
`dayStartUtc.AddDays(1)`, so the office day ended an hour early on the
25-hour GMT-transition Sunday and an hour late on the 23-hour BST-transition
Sunday.

`LondonCalendar.DayAndWeekBoundariesAt` now returns `DayStartUtc`, `DayEndUtc`
and `WeekStartUtc`, resolving all three through the same selected time zone.
`GetOperationsSnapshot` passes that `DayEndUtc` into the needs-attention
projection; the local `AddDays(1)` boundary calculation is deleted. The
existing transition-Sunday calendar tests now assert the returned day end, and
`DashboardBoundaryTests.NeedsAttentionIncludesTheLastHourOfTheGmtTransitionSundayInToday`
pins the real production caller at 23:30Z. No assertion was deleted, skipped,
weakened or inverted.

### [minor] The UTC fallback was widened to every calendar consumer — FIXED

Confirmed. Plan step 2's earlier phrase "preserve semantics" was inaccurate:
the fallback was preserved for the dashboard but silently widened for
`StartOfDay`, `StartOfNextDay`, `DateAt` and `ToUtc` callers.

Those public primitives now call `GetTimeZone`, which directly resolves
`Europe/London` and surfaces a missing or invalid zone. The named-exception
fallback remains only inside `ResolveTimeZone`, and only
`DayAndWeekBoundariesAt` calls it. This preserves the dashboard's supported UTC
fallback without silently degrading case search or chase scheduling. There is
no catch-all or suppressed error.

### [minor] Ticket documents and commit traceability were stale — FIXED

Confirmed. This round updates the files, plan, post-implementation report and
scratch records. The focused filter now records its real result: Failed: 0,
Passed: 16, Skipped: 0. The ticket's commits now include the previously omitted
round-3 merge `44bcb8c0f622e5f169bc0eb43d7271f1632b7d8d` and remediation
`1e15e8325cc74ccfb5d8f059b1a5a17c20e98aad`.

### [info] `DayAndWeekBoundariesAt` has one production caller — ACCEPTED

No change. This method relocates an already-called dashboard rule; it is not a
new speculative API. [[PLAT-051]] remains the intended second caller and is not
claimed as delivered by this ticket.

### [info] Remaining rules sweep — NO ACTION

The verifier found no assertion tampering, scope breach, inert UI, operator
copy, fabricated fixture, duplicate label list, test-only delivered code or
catch-all suppression. The round-3 diff preserves those results.

## Simplification pass — 2026-08-29

- Reuse: extended `LondonCalendar.DayAndWeekBoundariesAt` and the existing
  `GetOperationsSnapshot` call rather than adding a calendar type or service.
- Simplification: one returned tuple carries the complete day/week boundary;
  the duplicate 24-hour calculation is deleted.
- Efficiency: the boundary method resolves one zone and reuses it for all three
  UTC conversions; no cache, dependency or configuration was added.
- Altitude: Core remains the policy owner and Operations remains only a caller.

No unapplied simplification findings.

## Verification after round 3

- `dotnet build ./Pegasus.slnx --configuration Release` — exit code 0,
  0 warnings, 0 errors.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~LondonCalendarTests|FullyQualifiedName~DashboardBoundaryTests"`
  — exit code 0; Failed: 0, Passed: 16, Skipped: 0.
