---
id: PLAT-060
type: ticket
title: >-
  Fold the four remaining Europe/London zone lookups into LondonCalendar — one
  conversion owner repo-wide
status: backlog
area: platform-operations
order: 810
assignee: ''
profile: fix
labels:
  - backend
  - tech-debt
groups:
  - EPIC-011
links:
  - PLAT-054
archived: false
created: '2026-08-29T08:11:57.357Z'
updated: '2026-09-03T15:15:28.540Z'
---

## What

[[PLAT-054]] made `src/Pegasus.Core/LondonCalendar.cs` the owner of the
`Europe/London` id, the office day/week boundary and the missing-zone → UTC
fallback for `GetOperationsSnapshot`. It did **not** make that ownership
repo-wide: four further `Europe/London` lookups remain, all outside PLAT-054's
`Owns` list and therefore deliberately untouched by it. Two of them re-implement
behaviour `LondonCalendar` already owns.

Raised by the PLAT-054 adversarial verifier (2026-08-28), which established that
the PLAT-054 report named only the first of the four.

## The four call sites

| # | Site | What it duplicates |
| --- | --- | --- |
| 1 | `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:80` | Private `LondonTimeZone` field, **no fallback**. Already imports `LondonCalendar` (`ToUtc` at :84) but keeps its own lookup for the instant→local direction. |
| 2 | `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:23` | Same private field, no fallback. |
| 3 | `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs:22` | Same private field, no fallback. |
| 4 | `src/Pegasus.Web/Presentation/OperatorLabels.cs:650` (`InOffice`) | A second, byte-equivalent implementation of the `TimeZoneNotFoundException or InvalidTimeZoneException` → `TimeZoneInfo.Utc` fallback — under a doc comment that reads "The one conversion." |

## The missing primitive

All four want **instant → office-local**, which `LondonCalendar` does not expose
publicly. It has `DateAt(DateTimeOffset)` (instant → `DateOnly`) but nothing
returning the local `DateTimeOffset`/`DateTime` that sites 1–4 need.

PLAT-054 deliberately did **not** add that primitive: with every caller outside
its `Owns` list it would have shipped as an abstraction with no production
caller, which is the exact rule-14 defect the verifier raised against
`ToUtcRange` in the same review. This ticket owns the callers, so it can add the
primitive and wire all four in one diff.

## Watch for

- Sites 2 and 3 have **no** fallback today, so adopting `LondonCalendar` changes
  their behaviour on a host with no IANA database: throw-at-static-init becomes
  fall-back-to-UTC. That is the intended direction, but it is a behaviour change
  and needs saying in the report, not glossing.
- Site 4's `InOffice` is on the operator display path; keep the rendered output
  identical and prove it with the existing `OperatorLabels` tests.
- `OperatorLabels.cs` is PLAT-029's file in the EPIC-011 wave map — sequence
  this after that lane, or split site 4 out.

## Owns

`src/Pegasus.Core/LondonCalendar.cs`, `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs`,
`src/Pegasus.Web/Presentation/OperatorLabels.cs`, and their tests.

## Acceptance

- `grep -rn "Europe/London" --include=*.cs src/` returns hits only in
  `LondonCalendar.cs`.
- `grep -rn "FindSystemTimeZoneById" --include=*.cs src/` returns one hit,
  in `LondonCalendar.ResolveTimeZone`.
- Build and the affected focused test filters green.
