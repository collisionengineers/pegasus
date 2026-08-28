## Files touched

- `src/Pegasus.Core/LondonCalendar.cs` — extend the existing calendar owner
  (not a new `OfficeCalendar` type) with the office day/week boundary and the
  half-open range conversion.
- `src/Pegasus.Core/Operations/OperationsSnapshot.cs` — delete the private
  `OfficeTimeZoneId` constant and `OfficeBoundaries` method; call
  `LondonCalendar` instead.
- `tests/Pegasus.Core.Tests/LondonCalendarTests.cs` — extend the existing
  calendar test file with the new coverage (BST/GMT transition days, Monday
  week start, half-open range, reversed range).

## Files read, not touched

- `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs` — a third private
  `Europe/London` zone lookup exists here (`LondonTimeZone` field, line 80).
  Confirmed present; out of scope for this ticket (not named in "Owns"),
  reported rather than fixed.
- `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` — existing
  behavioural coverage for `GetOperationsSnapshot`'s day/week boundaries; left
  unmodified since none of its cases fall on a DST transition day.
