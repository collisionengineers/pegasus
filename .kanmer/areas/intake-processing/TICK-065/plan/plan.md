## Simplification pass (2026-08-20, before PR)

Diff is 6 edited files + 2 new files, +85/-0 lines. Reviewed for reuse,
simplification, efficiency, and altitude:

- **Reuse.** `ImageIntakeChaseSchedule.IsChaseDue` calls
  `CaseChaseSchedule.FirstChaseAt` directly rather than re-deriving the
  seven-day/London-time rule; `OperatorLabels.ImageChaseState` reuses the
  literal "Chase due" string `ChaseState` already emits; the Web change reuses
  the existing `Shared/_StatusChip` partial and the existing Not-ready image
  table/row loop — no new page, query, or partial.
- **Simplification.** Considered a persisted `ImageIntakeDueWork`
  table/migration mirroring `CaseDueWork` and rejected it: the value being
  displayed (due/not-due) is a pure function of `RegisteredAtUtc`, so
  persisting it would be state that can drift from its own source, exactly the
  failure mode `ImageIntakeContracts.cs` already deliberately avoids for the
  pairing-visibility half. No migration, no new store, no new interface.
- **Efficiency.** No new query: `ImageInitiatedRows` was already loaded by
  `LoadNotReadyAsync`; the chase-due read is computed in Razor from a field
  already on the row (`RegisteredAtUtc`) and the page's existing injected
  `TimeProvider`.
- **Altitude.** `_StatusChip.cshtml`'s tone switch had "chase due" / "chasing
  paused" / "chasing stopped" completely unmapped (silently neutral) before
  this change — fixed as part of adding the image-side entries rather than
  leaving a known gap next to new code that depends on the same switch.

No findings left undisposed. Nothing deferred.
