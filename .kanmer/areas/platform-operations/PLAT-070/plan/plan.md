# Plan — PLAT-070 (2026-09-03, gpt-5.6-terra high, Sonnet-verified)

Base: `origin/dev` @ `897db9530a45063e8f684f2800685afbfdced006`. Serial before
[[CASE-038]] (shared lock on `Pages/Cases/Details.cshtml`).

D44 removes the staff "review instructions/images" act — its config-gated
readiness rule (both the image-named and instruction-named halves — the
ticket's own grep example names only the image half, but D44's wording ("no
staff act of reviewing instructions or images") and the verified Core code
both cover both), and every UI surface for it: the Workflow-configuration
panel and the Case-page "Confirm completeness" form's two staff-reviewed
checkboxes. D45 is a governing-documents-only change (no ENG-035/ENG-036
engineering work). Binding design rules: no explanatory copy; visible labels
only through `src/Pegasus.Web/Presentation/OperatorLabels.cs`; exact states
`Not ready`, `Review`, `With Engineer`; a retired control is absent, never
rendered disabled.

## Verified scope correction (read this before touching `CaseCompleteness`)

`files/files.md` recommends deleting `CaseCompleteness.InstructionConfirmedByStaff`
/ `ImagesConfirmedByStaff` outright. A read-only check during planning found
that would break the build and materially expand the diff, so **this plan
does not delete those two properties**:

- `src/Pegasus.Web/Pages/Cases/Create.cshtml(.cs)` — **not an owned path, not
  named in the ticket, not part of EPIC-012's Case-workspace redesign** —
  binds and constructs `CaseCompleteness` with real
  `InstructionConfirmedByStaff`/`ImagesConfirmedByStaff` values at case
  creation (`asp-for` + positional constructor args). Deleting the properties
  breaks this page.
- The same two properties are also referenced, verified live, by raw SQL
  `INSERT INTO Cases (...)` fixtures and additional `CaseCompleteness`
  constructions across roughly 15 more test files never mentioned in
  `files.md` (e.g. `RailCountsWebTests.cs`, `DueChaserSweepPersistenceTests.cs`,
  `VehicleLookupGapFillTests.cs`, `ImageIntakeWebTests.cs`,
  `CaseCreateWebTests.cs`, and others) — a runtime SQL blast radius no
  `dotnet build` would catch, only discovered by grepping the column name
  directly. None of these files are owned by PLAT-070.

Instead, verified against the actual call graph
(`src/Pegasus.Web/Pages/Cases/Shared/_ReadinessHiddenFields.cshtml` and
`_CaseWorkflow.cshtml`'s "Confirm completeness" form): the two properties
remain on `CaseCompleteness`, still populated once at intake by (out-of-scope)
`Create.cshtml`. What's actually removed within owned files is:

1. The two `CaseCompleteness` values' role as a **readiness/policy gate**
   (`CaseLifecycleRules.ValidateReadiness`, `CaseCompletenessPolicy.Evaluate`)
   — becomes completeness-only, matching D44's literal sentence "Not ready →
   Review is decided by completeness only".
2. Their role as an **editable checkbox** on the Case page's "Confirm
   completeness" form (`_CaseWorkflow.cshtml` — the "Instructions
   staff-reviewed" / "Images staff-reviewed" checkboxes named verbatim in the
   ticket) and the corresponding posted-parameter handling in
   `Details.cshtml.cs`.
3. The entirely separate `CaseReadinessEvidence.InstructionsReviewedByStaff`
   / `ImagesReviewedByStaff` values and `CaseWorkflowConfiguration`'s two
   `RequireStaff...BeforeEngineerAssignment` flags — deleted outright (no
   external caller outside owned files; verified by grep — the only matches
   are already-owned files).

Net effect: after this ticket, `CaseCompleteness.InstructionConfirmedByStaff`
/ `ImagesConfirmedByStaff` exist only as intake-time data Create.cshtml still
captures; they no longer gate anything and are no longer staff-editable
post-creation. This is a real residual gap against D44's literal "no
checkbox... anywhere" — record it as a follow-up ticket for Create.cshtml
(out of PLAT-070's owned paths) rather than silently expanding this one.

## Steps

1. **Core — contracts and the readiness gate.** Reuse
   `CaseLifecycleRules.ValidateReviewReadiness` as the sole completeness-only
   evidence check (already exists; `ValidateReadiness` currently duplicates
   its evidence check plus the config-gated OR-clauses).
   - `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` — delete
     `CaseWorkflowConfiguration.RequireStaffInstructionReviewBeforeEngineerAssignment`
     / `RequireStaffImageReviewBeforeEngineerAssignment` (record becomes
     `(string PolicyKey, int PolicyVersion)`); delete
     `CaseReadinessEvidence.InstructionsReviewedByStaff` /
     `ImagesReviewedByStaff` (record becomes `(bool InstructionsComplete, bool
     ImagesComplete, string EvidenceReference)`).
   - `src/Pegasus.Core/Workflow/DefaultCaseWorkflowConfiguration.cs` — drop
     the two default flags from its output.
   - `src/Pegasus.Core/Workflow/WorkflowConfigurationAdministration.cs` —
     drop the two flags from `UpdateWorkflowConfigurationRequest`.
   - `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` — `ValidateReadiness`
     keeps its `configuration.PolicyKey`/`PolicyVersion` checks (unrelated to
     review, still required by `ValidateAssignment`) but delegates the
     evidence check to `ValidateReviewReadiness(evidence)` instead of
     repeating it with the now-deleted config-gated OR-clauses — reuse over a
     second copy of the same completeness check.
   - `src/Pegasus.Core/Cases/CaseContracts.cs` — `CaseCompleteness.IsReadyForReview`
     becomes `InstructionComplete && ImagesComplete` (drop the
     `automaticallyDefinitive || (InstructionConfirmedByStaff &&
     ImagesConfirmedByStaff)` branch). Do **not** remove the
     `InstructionConfirmedByStaff`/`ImagesConfirmedByStaff` properties (see
     scope correction above). The `automaticallyDefinitive` parameter becomes
     unused inside this method but stays on the signature — removing it
     would additionally touch `src/Pegasus.Core/Intake/AcceptIntake.cs`
     (unowned); leave it and flag as a follow-up simplification, not new
     scope.
   - `src/Pegasus.Core/Cases/CaseDataOperations.cs` —
     `CaseCompletenessPolicy.Evaluate`'s `satisfiesPolicy` formula drops the
     `configuration.RequireStaff...` / `completeness...ConfirmedByStaff`
     gated clause entirely, becoming `completeness.InstructionComplete &&
     completeness.ImagesComplete`. `CaseDataPolicy.ValidateCompleteness`
     (the "confirmed implies complete" guard) is untouched — it still guards
     data the intake page writes.

2. **Infrastructure — persisted workflow configuration only.** Reuse the
   existing `AdministrationPolicyModelConfiguration`/
   `EfWorkflowConfigurationStore` and EF migration convention. This is
   narrower than `files.md`: no case-completeness persistence file changes
   (verified unnecessary — `CaseCompleteness`'s shape is unchanged, so
   `PegasusDbContext.cs`'s case-side mapping, `IntakeAllocationEntities.cs`,
   `EfCaseDataStore.cs`, `EfCaseAcceptanceStore.cs`,
   `EfIntakeAllocationStore.cs`, `EfLinkedCaseReplacementStore.cs`,
   `EfQueuedCustodyProcessor.cs`, and
   `src/Pegasus.Core/Intake/IntakeAllocation.cs` need no edits — leave them
   alone).
   - `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs`
     — delete the two `WorkflowConfigurationEntity` properties.
   - `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs`
     — delete the two seeded values.
   - `src/Pegasus.Infrastructure/Persistence/EfWorkflowConfigurationStore.cs`
     — delete the two flags from every mapping/replay/audit-snapshot member.
   - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_RemoveWorkflowConfigurationStaffReviewFlags.cs`
     (create) + matching `.Designer.cs` — drop only
     `WorkflowConfigurations.RequireStaffInstructionReviewBeforeEngineerAssignment`
     and `.RequireStaffImageReviewBeforeEngineerAssignment`. Do not touch the
     `Cases` table. Do not edit any historical migration.
   - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
     — match the post-migration model.
   - Reuse `scripts/Test-MigrationGrants.ps1`; no grant change expected — the
     migration only drops columns on a table the Worker role already has
     access to.

3. **Web — Case pages.** Reuse the shared `Readiness` helper in
   `CaseMutationPageModel` and the existing `_ReadinessHiddenFields.cshtml`
   partial; narrow both rather than replacing them.
   - `src/Pegasus.Web/Pages/Cases/Shared/_ReadinessHiddenFields.cshtml` —
     remove the `instructionsReviewedByStaff`/`imagesReviewedByStaff` hidden
     inputs (sourced from `Completeness.Values.InstructionConfirmedByStaff`/
     `ImagesConfirmedByStaff`); keep `instructionsComplete`,
     `imagesComplete`, `evidenceReference` unchanged.
   - `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — remove the
     two "Instructions staff-reviewed" / "Images staff-reviewed" checkboxes
     (and their trailing hidden-false inputs) from the "Confirm completeness"
     form; keep the "Instructions complete" / "Images complete" checkboxes
     and the Reason field. Remove the `instructionsReviewedByStaff`/
     `imagesReviewedByStaff` entries from the "Return to Review" dialog's
     posted-data dictionary.
   - `src/Pegasus.Web/Pages/Cases/Details.cshtml` — remove only the retired
     review-field UI; the frame itself is CASE-038's to redesign.
   - `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — the `ConfirmCompleteness`
     handler stops accepting `instructionConfirmedByStaff`/
     `imagesConfirmedByStaff` as posted mutation inputs (keeps
     `instructionComplete`/`imagesComplete`); the readiness-evidence
     construction drops the two `ConfirmedByStaff` args (3-arg
     `CaseReadinessEvidence` now); the field-name → label lookup helper
     drops its `"instructionConfirmedByStaff" or "instructionsReviewedByStaff"`
     / `"imagesConfirmedByStaff" or "imagesReviewedByStaff"` cases.
   - `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` — the shared
     `Readiness` factory drops its `instructionsReviewedByStaff`/
     `imagesReviewedByStaff` parameters and the two posted-field-name
     constants used for change-detection/audit.
   - `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs` — engineer-assignment
     and Send-to-EVA handlers drop the two review parameters they currently
     forward into `CaseReadinessEvidence` (review field handling only —
     everything else about Send to EVA/assignment is unchanged).
   - `src/Pegasus.Web/Pages/Cases/Closure.cshtml.cs` — reopen-to-Review
     handler drops the same two posted parameters.

4. **Web — Administration.** Reuse the existing
   `GetWorkflowConfiguration`/`UpdateWorkflowConfiguration` commands and
   authorization/versioning path; no new configuration route.
   - `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` — remove the
     `workflow-review-title` panel (`@OperatorLabels.WorkflowConfiguration.Review`
     heading and its two checkboxes) entirely; the rest of the configuration
     form is unaffected.
   - `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs` — remove
     the two bound properties and the two constructor args passed into
     `UpdateWorkflowConfigurationRequest`.
   - `src/Pegasus.Web/Presentation/OperatorLabels.cs` — delete
     `WorkflowConfiguration.Review`, `.InstructionReviewRequired`,
     `.ImageReviewRequired` (verify exact member names while editing; do not
     add replacement copy).

5. **Tests — update, don't weaken.** Reuse existing fixtures/harnesses.
   - `tests/Pegasus.Core.Tests/Lifecycle/CaseReviewReadinessTests.cs`,
     `AssignCaseEngineerTests.cs` — prove a case with complete instruction
     and images reaches Review/is assignable with no review evidence/flag.
   - `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs`,
     `CaseDataOperationsTests.cs` — replace the retired
     staff-confirmation-gated scenarios with completeness-only assertions
     (the `automaticallyDefinitive` distinction these tests currently exist
     to prove becomes moot — replace, don't just delete, so completeness-only
     behaviour stays covered).
   - `tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs` —
     trim `new CaseWorkflowConfiguration(false, false, "test", 1)` to the
     2-arg form.
   - `tests/Pegasus.Core.Tests/Identity/AdministrationPolicyTests.cs` —
     update `UpdateWorkflowConfigurationRequest` construction.
   - `tests/Pegasus.IntegrationTests/WorkflowConfigurationWebTests.cs` —
     assert no review panel/checkboxes render; preserve the route's other
     access/behaviour assertions.
   - `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs`
     — replace the retired update/replay assertions.
   - `tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs`,
     `CaseClosureWebTests.cs` — trim the 5-arg `new CaseReadinessEvidence(...)`
     calls to 3 args; remove posted review form values.
   - `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`,
     `CaseDataCompletenessPersistenceTests.cs`, `CaseMatchIntegrationTests.cs`,
     `AssessmentPersistenceIntegrationTests.cs`,
     `ProviderInspectionModeAcceptanceTests.cs`,
     `ProviderApiCaseDataSnapshotPersistenceTests.cs` — each has a
     `FixedConfiguration.Configuration = new CaseWorkflowConfiguration(...)`
     (verified present in all six); trim each to the 2-arg form. No other
     change expected in these six; confirm with `dotnet build`.

6. **Governing documents and the Administration snapshot.** Reuse
   `Update-TestUiSnapshots.ps1` and `Test-UiCatalogue.ps1`; accept only the
   one owned snapshot's diff.
   - `docs/design/test-ui/pages/administration-configuration--default.html`
     — regenerate; the review panel disappears from the capture.
   - `docs/frd/frd-01-case-identity-and-lifecycle.md` — replace the
     staff-review gate language with D44's completeness-only rule.
   - `docs/frd/frd-06-vehicle-and-engineering-evidence.md` — amend D39's
     damage-zone fields to severity/note only (D45; no zone `type`).
   - `docs/frd/frd-12-operator-experience.md` — record D44 (Workflow
     configuration, Case record readiness).
   - `docs/design/README.md` — remove the review panel from the Workflow
     configuration panel list; remove "Type" from the damage-diagram row.
   - `.kanmer/groups/EPIC-012/context.md` — D44/D45 already appear verbatim;
     confirm no further edit needed (read it during implementation before
     assuming a change is required).
   - `.kanmer/groups/EPIC-011/context.md` — add an explicit cross-reference
     that D44–D46 supersede any older EPIC-011 review/damage-type wording
     where they differ (EPIC-012's context.md already states this at the
     epic level; EPIC-011's own file does not yet say so).

## Must not touch

- `src/Pegasus.Web/Pages/Cases/Create.cshtml(.cs)` and every file that
  constructs `CaseCompleteness` for reasons unrelated to this ticket (see
  Verified scope correction) — a follow-up ticket's scope, not this one.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml` outside the named review-field
  deletion — CASE-038 owns the frame redesign (shared lock, serial after
  this ticket).
- Any Damage model/diagram/report/label/test owned by ENG-035/ENG-036 — D45
  is governing-documents-only here.
- Historical migrations, the EVA reference schema, and
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/**` (read-only mockup source).
- `Pages/Shared/*`, `Pages/Administration/Shared/*`, `site.css`, `site.js`.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
./scripts/Test-MigrationGrants.ps1
git grep -i "ReviewedByStaff\|RequireStaffImageReview\|RequireStaffInstructionReview\|staff-reviewed"
```

The final `git grep` must return nothing on the branch outside historical
migration files (which are never edited) — it must NOT be run against
`ConfirmedByStaff`, which legitimately survives this ticket on
`CaseCompleteness`/`Create.cshtml`.

## Acceptance conditions

- `ValidateAssignment`/`ValidateReadiness`/`ValidateReviewReadiness` and
  `CaseCompletenessPolicy.Evaluate` all gate Not ready → Review and
  engineer assignment on `InstructionComplete && ImagesComplete` alone; no
  code path reads `CaseWorkflowConfiguration.RequireStaff...` or
  `CaseReadinessEvidence...ReviewedByStaff` (both deleted, so this is a
  compile-time guarantee).
  `CaseCompleteness.InstructionConfirmedByStaff`/`ImagesConfirmedByStaff`
  still exist and are still set by `Create.cshtml` at intake — unchanged,
  out of scope.
- `/Administration/Configuration` renders no "Staff review requirements"
  panel; the regenerated Test UI snapshot and `Test-UiCatalogue.ps1` confirm
  it; the rest of the configuration form still saves through the existing
  route.
- The Case page's "Confirm completeness" form offers only "Instructions
  complete"/"Images complete" + Reason; no "staff-reviewed" checkbox renders
  anywhere in the Case workspace.
- D44 and D45 are present, in the stated terms, in frd-01, frd-06, frd-12,
  `docs/design/README.md`, and both group `context.md` files.
- `dotnet restore --locked-mode`, `dotnet build --configuration Release`,
  the filtered `dotnet test`, both snapshot commands, the UI catalogue check,
  and `Test-MigrationGrants.ps1` all exit 0.

## Stop condition

Post-implementation report written; PR opened against `dev`; PLAT-070 moved
to Review. Do not merge the PR and do not start CASE-038 or any other
ticket. Stop and report — rather than silently expanding scope or silently
dropping the finding — if implementation turns up another caller of
`CaseWorkflowConfiguration`/`CaseReadinessEvidence` outside this file list,
or if `Create.cshtml`'s now-vestigial confirmation checkboxes need to be
addressed for this ticket to be accepted (that determination belongs to
review, not to silent scope growth here).
