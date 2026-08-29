## Files touched

- `src/Pegasus.Core/LondonCalendar.cs` — extend the existing calendar owner
  (not a new `OfficeCalendar` type) with the office day/week boundary
  (`DayAndWeekBoundariesAt`), carrying the Monday week-start rationale that
  came off the deleted `OperationsSnapshot.OfficeBoundaries`.
- `src/Pegasus.Core/Operations/OperationsSnapshot.cs` — delete the private
  `OfficeTimeZoneId` constant and `OfficeBoundaries` method; call
  `LondonCalendar` instead.
- `tests/Pegasus.Core.Tests/LondonCalendarTests.cs` — extend the existing
  calendar test file with the new coverage (BST/GMT transition days, Monday
  week start, and the two transition-Sunday cases that pin the changed
  `DayAndWeekBoundariesAt` production path).

## Not shipped — `ToUtcRange` withdrawn (round 2)

The first round added `LondonCalendar.ToUtcRange(from, toInclusive)` as the
half-open range seam for [[PLAT-051]]. It shipped with **zero production
callers** — PLAT-051 is Wave 4 and has not started — so it was test-only code
against rule 14 ("done means wired") and the no-abstraction-without-a-second-
concrete-caller rail. It and its two tests are removed. PLAT-051 has a working
production precedent for the same half-open range built from the primitives
that do have callers: `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:79-80`
and `:92` pair `LondonCalendar.StartOfDay` with `LondonCalendar.StartOfNextDay`.

## Files read, not touched — the four remaining `Europe/London` lookups

`grep -rn "Europe/London" --include=*.cs .` finds four zone lookups outside
`LondonCalendar`, **all** outside this ticket's `Owns` list. The first round
reported only the first of them; the full list is now tracked as [[PLAT-060]].

- `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:80` — private `LondonTimeZone`
  field, no fallback.
- `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:23` — same,
  no fallback.
- `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs:22` —
  same, no fallback.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs:650` (`InOffice`) — a second,
  byte-equivalent implementation of the missing-zone → UTC fallback this ticket
  claims to own, under a doc comment reading "The one conversion."

`src/Pegasus.Infrastructure/Persistence/EfCaseDueChaserStore.cs:256` also
matches the grep but is a message string, not a zone lookup.

- `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` — existing
  behavioural coverage for `GetOperationsSnapshot`'s day/week boundaries; left
  unmodified. Its fixtures are 2026-08-03/04/05, none on a DST transition day,
  so it does **not** cover the behaviour change this ticket makes — the two new
  `LondonCalendarTests` transition-Sunday cases do.
