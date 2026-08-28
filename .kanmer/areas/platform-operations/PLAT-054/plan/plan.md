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
