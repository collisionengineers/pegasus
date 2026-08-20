## Post-implementation report — TICK-065 (INT-32 completion)

### What shipped
1. **Chase state for the image half (new).** `ImageIntakeChaseSchedule.IsChaseDue`
   (`src/Pegasus.Core/ImageIntake/ImageIntakeChaseSchedule.cs`) is a pure
   derived read — due once `RegisteredAtUtc` has stood as long as a Not-ready
   Case's first chase (`CaseChaseSchedule.FirstChaseAt`, reused, not
   redefined). No held/stopped state, no persistence, no Worker sweep: images
   have no manual chase-pause machinery and this ticket does not add any.
   Surfaced as a new "Chase" column (`Shared/_StatusChip`, "Chase due"/"Not
   yet due") on the Triage → Not ready → Image-initiated table
   (`src/Pegasus.Web/Pages/Triage/Index.cshtml(.cs)`,
   `src/Pegasus.Web/Presentation/OperatorLabels.cs`). Fixed a pre-existing gap
   in `_StatusChip.cshtml`'s tone switch while there: "chase due"/"chasing
   paused"/"chasing stopped" were unmapped (silently neutral) for the
   Case-side chip too.
2. **Age — verified already satisfied, no code change.** The image table
   already showed `RegisteredAtUtc` under "Received"
   (`OperatorLabels.OfficeDate`), the same convention every other queue page
   in the app uses (a formatted date, never a computed relative age — verified
   with a repo-wide grep, zero hits for any relative-time formatting anywhere
   in `src/Pegasus.Web`). Documented in plan.md rather than inventing a new
   display format for one table.
3. **Pairing-ready notification — verified already satisfied, no code
   change.** `docs/capabilities.md:232`'s actual commitment is the derived
   `Associated with Case` visibility, not an active push notification.
   Confirmed in code: `EfImageIntakeStore`'s merge transition already writes a
   `CaseHistory` row (`image_initiated_case_merged`, labelled "Image-initiated
   Case merged in") in the same transaction as the merge, and
   `Cases/Index.cshtml.cs` already derives `Associated with Case`. Both
   predate this ticket (release 12 / INTK-008). No notifications subsystem
   was invented.
4. `docs/frd/frd-02-intake-and-source-identity.md` — added a paragraph
   documenting all three of the above next to the existing Image-initiated
   association section. `frd-06` — n/a, confirmed no touch needed.

### Reuse
- `CaseChaseSchedule.FirstChaseAt` (existing Core schedule policy) — not
  redefined.
- `OperatorLabels.ChaseState`'s "Chase due" wording — reused verbatim, not
  respelled.
- `Shared/_StatusChip` partial and the existing Not-ready Image-initiated
  table/row loop — no new page, query, or partial.

### Tests
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeChaseScheduleTests.cs` — 5
  tests (not due immediately, not due one tick before, due exactly at, due
  well past, reuses the exact Case-side instant). All pass.
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` —
  `NotReadyImageTableRendersChaseColumnForARecentRegistration` (new), plus the
  4 pre-existing facts in the file (including INTK-013's badge-count test),
  all pass: 5/5, ~54s (`SqlServer` trait, real DB).
- `tests/Pegasus.ArchitectureTests` — 97/97 pass (layering unaffected by the
  new Core file).
- `tests/Pegasus.Core.Tests` filtered to `ImageIntake|Tasks` — 105/105 pass.
- `dotnet build ./Pegasus.slnx -c Release --no-restore` — 0 warnings, 0
  errors (TreatWarningsAsErrors respected).

### Simplification pass
Recorded in plan.md under "Simplification pass (2026-08-20, before PR)" — no
undisposed findings.

### Deliberately left out
- No persisted due-work table/migration for the image half (a derived read is
  correct and sufficient; recorded reasoning in plan.md).
- No chaser-draft generation for images (`RunDueChasers` stays Case-only —
  explicitly out of scope per the lane instructions).
- No new notifications subsystem (verified unnecessary against the actual
  capability commitment).
- Dashboard's "Case work due" list (`Pages/Index.cshtml`) was not extended to
  include image-initiated rows — its row template is `CaseId`-linked, which an
  Awaiting-instruction Image-initiated record doesn't have; the Not-ready
  Image-initiated table is the parallel surface used instead (reasoning in
  plan.md).
- The Not-ready badge/count mismatch for image-initiated rows
  (`CaseStageCounts.NotReady`) is a separate, already-tracked concern
  (INTK-013/PLAT-003, confirmed landed on `dev` before this branch was cut) —
  not touched here.

### Parked questions
None.
