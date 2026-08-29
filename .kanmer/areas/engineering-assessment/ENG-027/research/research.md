# Research — ENG-027 Case valuations

## Existing patterns verified (read-only checks against `origin/dev`, merged before
this ticket's work started)

- `src/Pegasus.Core/Assessment/Estimates.cs` (merged by ENG-026) is the direct
  template for shape: a closed source vocabulary, an immutable details record,
  a policy class doing validation and actor rules, per-operation command
  interfaces + classes, and a list query. Confirmed by reading the file in
  full.
- `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs` is the
  template for the Infrastructure adapter: `Serializable` transaction, an
  operation-key replay guard (`FindReplayAsync`/`RequireExactReplay`),
  `CaseMutationGuard.RequireVersion`/`RequireLease`, `ArchivedCaseGuard`,
  and three history writes (`CaseWorkflowEvents`, `ActionHistory`,
  `CaseHistory`) per mutation. Event-kind naming convention confirmed as
  `estimate_created` / `estimate_updated` (not the ticket body's shorthand
  `valuation_recorded/amended` — the existing convention wins).
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260828112103_NamedEstimates.cs`
  and `20260828084644_GrantAiJobs.cs` are the migration templates: a
  `IsSqlServer()` guard, `RequireRuntimeRole` pre-check, and
  `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[<Table>] TO
  [pegasus_web_runtime_role];` in the same migration that creates the table
  (satisfies `scripts/Test-MigrationGrants.ps1`, confirmed by running it:
  exit 0, "83 migration files checked, every created table is granted or
  exempted").
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` `Get-MigrationPermissionMatrix`
  is the one list of expected runtime grants (the "bootstrap census"); it must
  gain a `CaseValuations` entry in the same diff as the migration (rule 16).
- `src/Pegasus.Core/LondonCalendar.cs` is the one conversion owner for
  Europe/London civil date/time → UTC instant (`ToUtc`); reused directly, no
  second conversion added (PLAT-054 is actively enforcing single ownership).
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` and
  `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` are
  silent on whether a recorded valuation may be edited or must be superseded.
  The binding UI contract (EPIC-011 `context.md` §1.8) draws a single "Edit"
  affordance per row, not a duplicate/version-new-row affordance, so edit-in
  -place (with `LastEditedBy`/`LastEditedAtUtc` plus full before/after
  `ActionHistory`) was used rather than the Estimates
  Draft/Accepted/Superseded state machine. `docs/capabilities.md` EXT-10's
  "revaluation history" wording is satisfied by the permanent
  `ActionHistory`/`CaseWorkflowEvents`/`CaseHistory` audit trail, not by a
  second live row per edit.
- `docs/capabilities.md` EXT-10 confirms scope: "Brought forward 2026-08-28
  (EPIC-011) as the Case workspace Valuations section (source, date, time,
  mileage, retail and trade values)."

## Scope boundary clarification (ticket body vs. the wave-3 task packet)

The ticket body's "What" section says Engineer's Value "writes the confirmed
`assessment.values.engineer` field through `ISaveAssessment` (single owner)".
That write-through is explicitly **out of scope for this ticket** per the
epic's wave plan: `waves.md` puts the Assessment "Send to Claude" dialog
(which needs `assessment.values.engineer`) in ENG-028 (wave 4), and this
ticket's own binding task packet says "Do not modify ... `AssessmentWorkspace
.cs`" and to add only a query port "since ENG-028 needs exactly that." This
ticket therefore delivers `IGetCurrentEngineersValue` as the named seam
ENG-028 will call to do that write-through itself; it does not call
`ISaveAssessment`. No second business implementation is introduced —
`ISaveAssessment` remains the single owner of `assessment.values.engineer`,
untouched by this diff.

## Naming deviation vs. ticket body

The ticket body names `IRecordCaseValuation`/`IAmendCaseValuation`. The
delivered ports are `ISaveValuation`/`IEditValuation`, matching the Estimates
naming (`ISaveEstimate`) rather than the ticket body's shorthand — "the
existing convention wins" (AGENTS.md simplicity rail). Behaviour matches the
ticket's requirements exactly; only the interface name differs from the
ticket's prose.
