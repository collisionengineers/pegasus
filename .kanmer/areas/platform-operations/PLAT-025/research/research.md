## Source of contract

EPIC-011 `context.md` §1.12 (Administration), verbatim for this area:

> Workflow configuration: Instruction completeness (2 checkboxes), Review
> (2 checkboxes), Due work (chase interval); Save configuration.

EPIC-011 `decisions-2026-08-29.md` D16 folds this ticket into EPIC-011 as
Wave 2 lane I2, owning `Pages/Administration/Configuration.*`.

## What exists today (verified by reading the code, not assumed)

- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml(.cs)` is the
  pre-redesign page: old classes (`back-link`, `split-main`, `detail-list`,
  `role-choices`, `form-panel`), no `.admin-layout` wrapper, no
  `_AdminNav` partial — it predates PLAT-029's shell.
- Core port `src/Pegasus.Core/Workflow/WorkflowConfigurationAdministration.cs`
  + `CaseWorkflowContracts.cs` (`CaseWorkflowConfiguration` record) expose
  **exactly two** administrator-editable booleans:
  `RequireStaffInstructionReviewBeforeEngineerAssignment` and
  `RequireStaffImageReviewBeforeEngineerAssignment` — both staff-review gates
  before Engineer assignment. There is no third or fourth configurable
  boolean anywhere in Core/Infrastructure for this policy
  (`grep -ril WorkflowConfig src/Pegasus.Core src/Pegasus.Infrastructure`).
- "Instruction completeness" / "image completeness" (`CaseReadinessEvidence.
  InstructionsComplete` / `ImagesComplete` in `CaseWorkflowContracts.cs`) are
  evidence flags computed per case, not administrator-configurable settings —
  there is no store, port, or column for a completeness *policy* the
  administrator can toggle.
- "Due work (chase interval)": `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs`
  models `CaseDueWork` per case with a `RemainingChaseInterval`, but the
  interval itself is a fixed constant (7 days) baked into scheduling logic —
  there is no admin-configurable global chase-interval setting, port, or
  persisted column anywhere in the codebase.
- `_AdminNav.cshtml` (PLAT-029, already merged/ported) already renders a
  "configuration" nav entry and expects `ViewData["AdminArea"] = "configuration"`
  from this page — confirmed by reading the partial's doc comment and markup.
- `Pages/Operations/Index.cshtml` (PLAT-023, merged to dev) is the closest
  already-ported example of the panel / `panel-head` / `section-label` /
  `table-wrap` house style this port reuses.
- `docs/design/README.md` §"No explanatory copy and page economy" bans hint
  sentences, "why this matters" prose, and how-it-works copy; there is no
  populated "closed necessary-copy list" of approved consequence sentences in
  that document, so the current page's `<aside class="notice">` ("Relaxing a
  gate applies to every case...") is not on any approved list and is a defect
  to remove, not preserve.

## Conclusion — a real backend gap, not a lane-fixable defect

The contract names four checkboxes and a chase-interval control; the backing
Core port supports only two booleans and no interval setting. Building the
other three controls for real would require a new Core port, a persistence
change, and a migration — explicitly out of scope for this ticket (EPIC-011
wave-2 lanes may not add a migration or a new Core port; migrations are
sequenced serially in wave 3). Rendering them without real backing would be
an inert/misleading control, which the epic's own rule forbids ("every drawn
control maps to a named handler ... never render an inert control").

**Disposition (AGENTS.md rule 22 / EPIC-011 D19):** defer the "Instruction
completeness" checkboxes and the "Due work (chase interval)" control to a new
ticket — this is the D19 last-resort case that legitimately applies, because
the work needs a new Core port + migration + an operator decision on what the
configurable completeness/interval policy should actually be (none of that is
written down anywhere; it cannot be inferred from the two-line contract
sentence). This ticket ports and re-skins the two real "Review" settings only,
through the unchanged Core port, and reports the gap.

## Reused (not rebuilt)

- Existing Core port `GetWorkflowConfiguration` / `UpdateWorkflowConfiguration`
  and its optimistic-concurrency + operation-key idempotency handling —
  unchanged.
- `AdministrationPageModel` base class, `StaffAuthorization` /
  `StaffAccessRight.ManageWorkflowConfiguration` gate — unchanged.
- `_AdminNav.cshtml` partial (read-only reuse, not modified).
- `OperatorLabels.Admin.Configuration` constant, already present.
- House style read from `Pages/Administration/Index.cshtml` and
  `Pages/Operations/Index.cshtml` (both already ported/merged).
