## What changed

- `src/Pegasus.Core/LondonCalendar.cs` — now the single owner of the
  `Europe/London` zone id (`TimeZoneId` const), the missing/invalid-zone → UTC
  fallback (`ResolveTimeZone`, catching `TimeZoneNotFoundException` and
  `InvalidTimeZoneException` exactly as `OperationsSnapshot` did), the Monday
  week-start rule (`DayAndWeekBoundariesAt`), and the half-open range
  conversion PLAT-051 needs (`ToUtcRange(from, toInclusive)`, throwing
  `ArgumentOutOfRangeException` on a reversed range).
- `src/Pegasus.Core/Operations/OperationsSnapshot.cs` — the private
  `OfficeTimeZoneId` constant and `OfficeBoundaries` method are deleted.
  `GetOperationsSnapshot.ExecuteAsync` now calls
  `LondonCalendar.DayAndWeekBoundariesAt(asOfUtc)`.
- `tests/Pegasus.Core.Tests/LondonCalendarTests.cs` — extended (not
  duplicated) with: a GMT date, a BST date, the BST-start transition day, the
  GMT-start transition day, the Monday week start, the half-open range, and
  the reversed-range rejection.

## Reuse

`LondonCalendar.StartOfDay`, `StartOfNextDay`, `DateAt`, `ToUtc` — the
existing conversion primitives — are the base every new method is built on.
No new calendar type was created; `OfficeCalendar` was explicitly rejected in
favour of extending `LondonCalendar`, per the ticket's own steer and the
one-list-per-concept rule.

## Deviation from "behaviour-preserving" — disclosed, not hidden

The old `OfficeBoundaries` computed day/week start by taking the *current
instant's* UTC offset (`local.Offset` at `asOfUtc`) and applying it to
midnight of the local calendar date:

```csharp
var local = TimeZoneInfo.ConvertTime(asOfUtc, office);
var dayStartLocal = new DateTimeOffset(local.Date, local.Offset);
```

On the BST-start and GMT-start transition days themselves, this is wrong when
"now" is evaluated after the transition point: it carries the post-transition
offset back onto midnight, which is still in the pre-transition offset. The
new `LondonCalendar.StartOfDay` resolves each local instant's own
invalid/ambiguous-time rules (`IsInvalidTime`/`IsAmbiguousTime`) rather than
reusing an unrelated instant's offset, so it returns the correct UTC instant
for midnight on a transition day.

This is a one-line behavioural fix folded into the "move it" ticket, not a
byte-identical port. It was not requested as a separate scope, is covered by
the new `BstStartDateHasTwentyThreeHourUtcRange` /
`GmtStartDateHasTwentyFiveHourUtcRange` tests, and does not change any
non-transition-day result (verified by the unmodified, still-passing
`DashboardBoundaryTests`, none of whose fixed dates falls on a transition
day). Flagging it explicitly rather than presenting it as identical.

## Build

`dotnet build ./Pegasus.slnx --configuration Release` — exit code 0, 0
warnings, 0 errors. Re-run independently by the verifying agent with the same
result.

## Tests

`dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
--configuration Release --no-build --filter
"FullyQualifiedName~LondonCalendarTests|FullyQualifiedName~DashboardBoundaryTests"`
— Passed: 10, Failed: 0, Skipped: 0. Re-run independently with the same
result.

## Out-of-scope defect found (reported, not fixed)

`src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:80` declares its own private
`Europe/London` `TimeZoneInfo` lookup (`LondonTimeZone` field) with no
fallback and no shared owner. It is outside this ticket's `Owns` list
(`OperationsSnapshot.cs`, the new/extended calendar owner, and their tests)
and was left untouched. Worth its own one-list-per-concept ticket.

## Commits

- `c2bef9df` — `fix(core): centralize London office boundaries (PLAT-054)` —
  pushed to `origin/task/plat-054-office-boundaries`.

## Verification performed independently of the implementing agent

- `git status --porcelain=v1` clean; `git log --oneline origin/dev..HEAD`
  shows exactly the one commit; `git log --oneline
  origin/task/plat-054-office-boundaries..HEAD` empty (pushed).
- Diff confined to exactly the three files named in Owns/files.md — no
  `Pages/**`, no Infrastructure, no migration touched.
- Build and the focused test filter re-run independently with matching
  results (0/0/10 pass).
