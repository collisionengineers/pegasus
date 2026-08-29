## What changed

- `src/Pegasus.Core/LondonCalendar.cs` — now the owner of the `Europe/London`
  zone id (`TimeZoneId` const), the missing/invalid-zone → UTC fallback
  (`ResolveTimeZone`, catching `TimeZoneNotFoundException` and
  `InvalidTimeZoneException` exactly as `OperationsSnapshot` did) and the
  Monday week-start rule (`DayAndWeekBoundariesAt`) **for the operations
  dashboard path**. It is not yet the owner repo-wide — see "Scope of the
  ownership claim" below.
- `src/Pegasus.Core/Operations/OperationsSnapshot.cs` — the private
  `OfficeTimeZoneId` constant and `OfficeBoundaries` method are deleted.
  `GetOperationsSnapshot.ExecuteAsync` now calls
  `LondonCalendar.DayAndWeekBoundariesAt(asOfUtc)`.
- `tests/Pegasus.Core.Tests/LondonCalendarTests.cs` — extended (not
  duplicated) with: a GMT date, a BST date, the BST-start transition day, the
  GMT-start transition day, the Monday week start, and the two
  transition-Sunday cases that pin the changed production path.

## Reuse

`LondonCalendar.StartOfDay`, `StartOfNextDay`, `DateAt`, `ToUtc` — the
existing conversion primitives — are the base every new method is built on.
No new calendar type was created; `OfficeCalendar` was explicitly rejected in
favour of extending `LondonCalendar`, per the ticket's own steer and the
one-list-per-concept rule.

## Scope of the ownership claim — corrected in round 2

Round 1 of this report claimed `LondonCalendar` is "the single owner of the
`Europe/London` zone id" and reported one out-of-scope duplicate. That was
wrong, and the acceptance criterion in the plan ("LondonCalendar is the single
owner of the `Europe/London` id … the UTC fallback") is **not** met repo-wide.
`grep -rn "Europe/London" --include=*.cs .` finds **four** zone lookups outside
`LondonCalendar`:

| # | Site | State |
| --- | --- | --- |
| 1 | `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:80` | reported in round 1 |
| 2 | `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:23` | **not** reported in round 1 |
| 3 | `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs:22` | **not** reported in round 1 |
| 4 | `src/Pegasus.Web/Presentation/OperatorLabels.cs:650` (`InOffice`) | **not** reported in round 1 — and it carries a second, byte-equivalent copy of the missing-zone → UTC fallback, under a doc comment reading "The one conversion." |

All four are outside this ticket's `Owns` list, so none is fixed here. They are
now tracked in full as [[PLAT-060]] rather than left as one under-stated
sentence. What this ticket actually delivers is the dashboard path's conversion
folded into `LondonCalendar`, plus an accurate map of what remains.

## `ToUtcRange` withdrawn — rule 14

Round 1 added `LondonCalendar.ToUtcRange(from, toInclusive)` as the half-open
range seam for [[PLAT-051]]'s Reports page, and its risks section said "No
other risks". It shipped with **zero production callers**: PLAT-051 is Wave 4
and has not started, so the only references were its own two tests. That is
test-only code against rule 14 ("done means wired") and against the
no-abstraction-without-a-second-concrete-caller rail.

It is removed, together with its two tests. Removing those two assertions is
not an assertion weakened to make a suite pass — the method under test no
longer exists; the coverage they provided is gone because the code they covered
is gone.

PLAT-051 is not blocked by this. The same half-open range already has a
production precedent built from primitives that *do* have callers:
`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:79-80` and `:92`
pair `LondonCalendar.StartOfDay` with `LondonCalendar.StartOfNextDay`. If
PLAT-051 finds it wants the named range helper after all, it can add it with
its own caller in the same diff — which is where it belonged.

## Behaviour change — restated accurately (round 1 under-stated it)

The old `OfficeBoundaries` computed the day and week start by taking the
*current instant's* UTC offset (`local.Offset` at `asOfUtc`) and applying it to
midnight of the local calendar date:

```csharp
var local = TimeZoneInfo.ConvertTime(asOfUtc, office);
var dayStartLocal = new DateTimeOffset(local.Date, local.Offset);
var weekStartLocal = dayStartLocal.AddDays(-daysSinceMonday);
```

On the two DST transition Sundays, asked after the transition point, this
carries the post-transition offset back onto a midnight that was still in the
pre-transition offset. `LondonCalendar.StartOfDay` instead resolves each local
instant's own invalid/ambiguous-time rules, so it returns the correct UTC
instant.

Round 1 called this "a one-line behavioural fix" to the day boundary. It is
**two** fields, not one: `weekStartLocal` is derived from `dayStartLocal` by
`DateTimeOffset.AddDays`, which preserves the offset, so the week boundary
moves by the same hour. Both `DayStartUtc` and `WeekStartUtc` — the two values
`GetOperationsSnapshot` feeds to `IDashboardQueries` — shift. Measured against
the real zone:

| asOf (UTC) | Old day / week | New day / week |
| --- | --- | --- |
| 2026-10-25T12:00Z | 2026-10-25T00:00Z / 2026-10-19T00:00Z | 2026-10-24T23:00Z / 2026-10-18T23:00Z |
| 2026-03-29T12:00Z | 2026-03-28T23:00Z / 2026-03-22T23:00Z | 2026-03-29T00:00Z / 2026-03-23T00:00Z |
| 2026-08-05T11:00Z | unchanged | unchanged |
| 2026-10-25T00:30Z | unchanged | unchanged |

The new values are correct in every case (London midnight on 2026-10-25 is
2026-10-24T23:00Z, BST still being in force until 02:00 local). **The fix is
kept** rather than reverting to byte-identical behaviour: the old values were
simply wrong, and reinstating them to preserve "behaviour-preserving" would be
shipping a known defect for the sake of a ticket phrase.

### Coverage of the changed path — round 1's claim was false

Round 1 said the change was "covered by the new
`BstStartDateHasTwentyThreeHourUtcRange` / `GmtStartDateHasTwentyFiveHourUtcRange`
tests". It was not: those two call `StartOfDay`/`StartOfNextDay` directly, and
the only `DayAndWeekBoundariesAt` test used 2026-08-05, a non-transition day.
`DashboardBoundaryTests`' fixtures are 2026-08-03/04/05. Nothing exercised the
changed production path on a transition day.

Two tests now do:

- `DayAndWeekBoundariesOnTheGmtTransitionSundayUseBstMidnights`
- `DayAndWeekBoundariesOnTheBstTransitionSundayUseGmtMidnights`

Both assert on `DayAndWeekBoundariesAt` — the method `GetOperationsSnapshot`
actually calls — and on both fields. **Proven to fail against the old
algorithm**: `DayAndWeekBoundariesAt` was temporarily reverted to the old
`local.Offset` math, rebuilt, and the filter re-run:

```
Failed LondonCalendarTests.DayAndWeekBoundariesOnTheGmtTransitionSundayUseBstMidnights
   Expected: 2026-10-24T23:00:00.0000000+00:00
   Actual:   2026-10-25T00:00:00.0000000+00:00
Failed LondonCalendarTests.DayAndWeekBoundariesOnTheBstTransitionSundayUseGmtMidnights
   Expected: 2026-03-29T00:00:00.0000000+00:00
   Actual:   2026-03-28T23:00:00.0000000+00:00
Failed!  - Failed: 2, Passed: 8, Skipped: 0, Total: 10
```

The old algorithm was then discarded and the shipped code restored. The same
run confirms the verifier's point: `DayAndWeekBoundariesStartTheWeekOnMonday`
and all three `DashboardBoundaryTests` passed against the old code, so they
never covered this.

## Build

`dotnet build ./Pegasus.slnx --configuration Release` — exit code 0, **0
warnings, 0 errors**.

One run in this round failed with `MSB3027`/`MSB3021` ("could not copy
Pegasus.Core.dll … locked by .NET Host (45844)"). That was a stale reusable
MSBuild node holding the output, not a compile error; `dotnet build-server
shutdown` cleared it and the rebuild was clean. Recorded because a non-zero
exit code is never quietly dropped.

## Tests — real numbers

`dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
--configuration Release --no-build --filter
"FullyQualifiedName~LondonCalendarTests|FullyQualifiedName~DashboardBoundaryTests"`
— **Failed: 0, Passed: 10, Skipped: 0**.

The total is 10 in both rounds, but the composition changed: round 1 was 2
pre-existing + 5 new `LondonCalendarTests` + 3 `DashboardBoundaryTests`; round
2 is 2 pre-existing + 5 `LondonCalendarTests` (the 2 `ToUtcRange` tests removed,
2 transition-Sunday tests added, net 0) + 3 `DashboardBoundaryTests`. Stating
this because an unchanged headline number across a changed test set is exactly
the kind of figure that should not be presented as "no change".

Also run this round, both unchanged by this ticket and green:

- `Pegasus.Core.Tests` (whole project, no filter) — **Failed: 0, Passed: 1126,
  Skipped: 0**.
- `Pegasus.ArchitectureTests` — **Failed: 0, Passed: 100, Skipped: 0**.

## Commits

- `c2bef9df` — `fix(core): centralize London office boundaries (PLAT-054)`.
- round-2 remediation commit — see the ticket's `commits` field.
