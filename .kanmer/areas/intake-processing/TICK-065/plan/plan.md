## Plan — TICK-065 (INT-32 completion)

Scope is the three gaps research.md identified: (a) image-half age, (b) image-half
chase state, (c) pairing-ready notification. Each is resolved by verifying what
already exists against the actual capability commitment (`docs/capabilities.md:232`)
before adding anything, per the lane's "check what INT-32 actually commits to; do
not invent a notification subsystem without an owner" instruction.

### (a) Age — already satisfied, no code change

`ImageIntakeSummary.RegisteredAtUtc` is already rendered on the Not-ready image
table (`Triage/Index.cshtml:265`, `OperatorLabels.OfficeDate(item.RegisteredAtUtc)`
under a "Received" column). Checked the instruction-initiated Not-ready table
(same page, `:222`) and every other queue/list page in `src/Pegasus.Web/Pages`
(`grep -rn "days ago|DaysSince|TimeAgo|RelativeTime" src/Pegasus.Web` — zero hits):
the app-wide convention is a formatted timestamp, never a computed relative age
("14 days", "3d ago"). Building a relative-age format for only the image table
would be a second, unprecedented vocabulary for the same fact the existing
Received column already states — the simplicity rail "the existing convention
wins" cuts the other way here. **Disposition: no change; the existing Received
column is the age signal, consistent with every other page.**

### (b) Chase state — new, minimal, derived (no new table)

The case-side chase machinery (`CaseDueWork`, `ICaseDueWorkQueries`,
`RunDueChasers`) is not reusable as-is: `CaseDueWork` rows are 1:1 with
`CaseWorkflow` (FK'd to a real Case) and carry Held/Stopped states driven by
manual staff holds and chaser-draft generation — none of which apply to a
pre-Case Image-initiated record. Extending `CaseDueWork`'s population to image
halves would mean making its Case-scoped columns (Reference via Case join,
manual hold/stop) conditional for a different entity — a bigger, more invasive
change than this ticket's actual ask, and the FK shape does not fit. Per the
"parallel minimal projection" fallback the lane instructions name, and
matching `ImageIntakeContracts.cs`'s own stated philosophy for the pairing
half ("derived, never independently set"), the chase-due read is a **pure
computed value**, not a persisted table:

1. `ImageIntakeChaseSchedule.IsChaseDue(registeredAtUtc, asOfUtc)` in
   `Pegasus.Core.ImageIntake` — reuses `CaseChaseSchedule.FirstChaseAt` (the
   same seven-calendar-day London-time schedule the case side already owns)
   instead of a second cadence constant. No held/stopped state: images have no
   manual chase machinery to pause or stop, so a two-value due/not-due read is
   the correct — not truncated — shape for what exists.
2. `OperatorLabels.ImageChaseState(bool)` reuses the exact "Chase due" text
   `ChaseState(CaseDueWorkState.Scheduled)` already uses, plus a new "Not yet
   due" for the other state (two-state vocabulary, not a new taxonomy).
3. Surfaced as a "Chase" column (via the existing `Shared/_StatusChip`
   partial) on the Not-ready tab's Image-initiated table — the queue surface
   staff already use daily for image-initiated work, exactly parallel to how
   the Dashboard's "Case work due" list is the surface for formal-case chase
   state. Extending the Dashboard's `CaseDueWork` list itself was considered
   and rejected: that list's row template links via `CaseId`
   (`asp-route-id="@item.CaseId"`), which an Awaiting-instruction Image-initiated
   record does not have; forcing it in would require a second row shape inside
   one partial or a schema change, either of which is larger than this ticket.
4. `_StatusChip.cshtml`'s tone switch currently has no entries for "chase due"
   / "chasing paused" / "chasing stopped" (they silently fall to neutral) —
   fixing this benefits the pre-existing Case-side chip too, so it is folded
   in as a genuine one-line addition, not new scope: `"chase due"` → amber,
   `"chasing paused"` → amber, `"chasing stopped"` → neutral, `"not yet due"`
   → neutral.

No new migration, no new store, no new Worker sweep: `RunDueChasers` (chaser
**draft generation**) is confirmed out of scope — this ticket asks for chase
*state*, not automatic chaser drafting for images, and the lane instructions
explicitly forbid a second chase engine.

### (c) Pairing-ready notification — already satisfied, no code change

Checked `docs/capabilities.md:232`, the actual accepted commitment: "Pairing
visibility is the derived `Associated with Case` state across intake,
Image-intake, and case surfaces; each half keeps its own registered/received
chronology." It does not commit to an active push/toast notification.
Verified in code:
- `EfImageIntakeStore.TransitionAsync` (the `MergeAsync` path) already writes
  a `CaseHistory` row (`EventType = "image_initiated_case_merged"`) in the
  same transaction as the merge — `OperatorLabels.cs:321` already gives it the
  operator label "Image-initiated Case merged in", and `Cases/Details.cshtml`
  already renders `CaseHistory`. The merge is visible on the case's own
  timeline the moment it happens.
- `Cases/Index.cshtml.cs:209` already derives `Associated with Case` from the
  origin receipt for the Cases list.

Both are the "definitive pairing" visibility the capability line commits to,
and both were already live before this ticket (release 12, INTK-008). Building
a new notifications table/toast/banner would be inventing a subsystem with no
accepted owner (no notifications surface exists anywhere in the app — verified
no hits for a `Notifications` table/`INotify*` port). **Disposition: no code
change; verified and documented.**

### Reuse named per step
- Step (b).1 reuses `CaseChaseSchedule.FirstChaseAt` (existing Core policy).
- Step (b).2 reuses the `ChaseState`/`_StatusChip` label-and-chip convention
  already established for the case side.
- Step (b).3 reuses the existing Not-ready Image-initiated table and its
  existing per-row iteration; no new page, no new query.

### FRD
- `docs/frd/frd-02-intake-and-source-identity.md` — add a short paragraph next
  to the existing Image-initiated association section (~line 134) recording
  the derived chase-due read and that it shares the Case-side seven-day
  schedule without sharing its held/stopped machinery.
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` — **n/a**, this ticket
  touches intake/source-identity queue behaviour only, nothing in vehicle or
  engineering evidence.

### Tests
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeChaseScheduleTests.cs` —
  not due one tick before `FirstChaseAt`; due exactly at and past it; reuses
  the same London-time boundary `CaseChaseScheduleTests` (if present) already
  exercises for the case side, so a fixed UTC instant with a known local-time
  relationship is enough — no new time-zone test infrastructure.
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — extend
  `NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows` (or a new focused
  fact) to assert the rendered image-initiated row carries "Not yet due" for a
  freshly registered record (registered at `DateTimeOffset.UtcNow` inside the
  test, well inside the 7-day window — no fake-clock plumbing needed for this
  assertion).
- Full a11y/browser pass not required: the change adds one `<td>` using an
  already-audited shared partial (`_StatusChip`) inside an existing table,
  not a new interactive control or landmark.

### Simplification pass
Recorded under a dated heading in this plan after implementation, before the
PR opens, per the lane's mandatory pre-PR pass.
