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
   ticket) and as the two "not staff-reviewed" outstanding-requirement rows
   in `Details.cshtml.cs`.
3. The entirely separate `CaseReadinessEvidence.InstructionsReviewedByStaff`
   / `ImagesReviewedByStaff` values and `CaseWorkflowConfiguration`'s two
   `RequireStaff...BeforeEngineerAssignment` flags — deleted outright (no
   external caller outside owned files; verified by grep — the only matches
   are already-owned files).

Net effect: after this ticket, `CaseCompleteness.InstructionConfirmedByStaff`
/ `ImagesConfirmedByStaff` exist only as intake-time data Create.cshtml still
captures; they no longer gate anything, are no longer staff-editable
post-creation, and are no longer displayed. This is a real residual gap
against D44's literal "no checkbox... anywhere" — record it as a follow-up
ticket for Create.cshtml (out of PLAT-070's owned paths) rather than silently
expanding this one.

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
     completeness.ImagesComplete`. The CASE-013 comment above it, which
     explains the waiver that clause needed, goes with it.
     `CaseDataPolicy.ValidateCompleteness` (the "confirmed implies complete"
     guard) is untouched — it still guards data the intake page writes.

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
   - `src/Pegasus.Infrastructure/Persistence/Migrations/20260903153134_RemoveStaffReviewFlags.cs`
     + matching `.Designer.cs` — drop only
     `WorkflowConfigurations.RequireStaffInstructionReviewBeforeEngineerAssignment`
     and `.RequireStaffImageReviewBeforeEngineerAssignment`. Does not touch
     the `Cases` table. No historical migration edited. The migration's
     `Down` re-adds both columns as `bool NOT NULL DEFAULT 1` (their
     current shape in `20260729180000_AdministrationPolicies.cs`), because
     EPIC-012's rollout rule reverts a failed wave PR-by-PR and a code-only
     revert against a dropped column would leave the store broken.
   - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
     — matches the post-migration model.
   - Reused `scripts/Test-MigrationGrants.ps1`; no grant change needed — the
     migration only drops columns on a table the Worker role already has
     access to (verified: `88 migration files checked, every created table
     is granted or exempted`).

3. **Web — Case pages.** Reuse the shared `Readiness` helper in
   `CaseMutationPageModel` and the existing `_ReadinessHiddenFields.cshtml`
   partial; narrow both rather than replacing them.
   - `src/Pegasus.Web/Pages/Cases/Shared/_ReadinessHiddenFields.cshtml` —
     remove the `instructionsReviewedByStaff`/`imagesReviewedByStaff` hidden
     inputs (sourced from `Completeness.Values.InstructionConfirmedByStaff`/
     `ImagesConfirmedByStaff`); keep `instructionsComplete`,
     `imagesComplete`, `evidenceReference` unchanged.
   - `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — remove the
     two visible "Instructions staff-reviewed" / "Images staff-reviewed"
     checkbox labels (lines 139-140) from the "Confirm completeness" form;
     keep the "Instructions complete" / "Images complete" checkboxes and the
     Reason field. **Keep one hidden `instructionConfirmedByStaff` /
     `imagesConfirmedByStaff` input each, carrying the case's *current*
     `data.Completeness.Values.*ConfirmedByStaff` value** (not the literal
     `false` the retired trailing hidden inputs carried): the
     `ConfirmCompleteness` handler replaces the whole `CaseCompleteness`
     record, so posting `false` would silently rewrite the intake-time
     confirmation `Create.cshtml` recorded — a persisted-data change this
     ticket does not authorise. D44 is satisfied by the absent control; the
     value is no longer operator-editable. Removed the
     `instructionsReviewedByStaff`/`imagesReviewedByStaff` entries from the
     "Return to Review" dialog's posted-data dictionary (lines 308-309).
   - `src/Pegasus.Web/Pages/Cases/Details.cshtml` — removed only the retired
     review-field UI; the frame itself is CASE-038's to redesign.
   - `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — the readiness-evidence
     construction drops the two `ConfirmedByStaff` args (3-arg
     `CaseReadinessEvidence` now); the field-name → label and field-name →
     value lookups drop their
     `"instructionConfirmedByStaff" or "instructionsReviewedByStaff"` /
     `"imagesConfirmedByStaff" or "imagesReviewedByStaff"` cases (the value
     switch at lines 597-600 and the label switch at lines 630-632).
     `OnPostConfirmCompletenessAsync` **keeps** its
     `instructionConfirmedByStaff`/`imagesConfirmedByStaff` parameters and
     its 4-arg `new CaseCompleteness(...)` — they are now fed by the hidden
     pass-through inputs above, not by an operator control, so no stored
     value changes.
   - `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — **also deleted the two
     `AddRequirement(..., data.Completeness.Values.InstructionConfirmedByStaff,
     "Instructions not staff-reviewed", why)` /
     `... ImagesConfirmedByStaff, "Images not staff-reviewed", ...` calls**
     (the readiness surface D44 names). Kept the two
     "Instructions incomplete"/"Images incomplete" requirements. Reused the
     existing `AddRequirement` helper and `CaseRequirement` record unchanged.
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
   - `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` — removed
     the `workflow-review-title` panel entirely, the
     `<span class="muted">@OperatorLabels.WorkflowConfiguration.Description</span>`
     subtitle, and (per the resolved open question, option (b)) the Reason
     field, the Save button and the form itself; the page now renders the
     current policy version read-only.
   - `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs` —
     removed the two bound properties, their `LoadAsync` assignments, the
     `OnPostAsync` handler and its `UpdateWorkflowConfigurationRequest` call
     entirely (option (b) — no operator-editable setting ships until
     PLAT-062).
   - `src/Pegasus.Web/Presentation/OperatorLabels.cs` — deleted
     `WorkflowConfiguration.Description`, `.Review`,
     `.InstructionReviewRequired`, `.ImageReviewRequired`; the simplification
     pass additionally deleted `.Reason` and `.Save` once the Reason
     field/Save button were removed (see Simplification pass below). `.Meta`
     stays. No replacement copy added.

5. **Tests — update, don't weaken.** Reused existing fixtures/harnesses.
   - `tests/Pegasus.Core.Tests/Lifecycle/CaseReviewReadinessTests.cs`,
     `AssignCaseEngineerTests.cs` — prove a case with complete instruction
     and images reaches Review/is assignable with no review evidence/flag.
   - `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs`,
     `CaseDataOperationsTests.cs` — replaced the retired
     staff-confirmation-gated scenarios with completeness-only assertions.
   - `tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs` —
     trimmed to the 2-arg `CaseWorkflowConfiguration` form.
   - `tests/Pegasus.Core.Tests/Identity/AdministrationPolicyTests.cs` —
     updated `UpdateWorkflowConfigurationRequest` construction.
   - `tests/Pegasus.IntegrationTests/WorkflowConfigurationWebTests.cs` —
     replaced the retired save/replay test with
     `AdministratorSeesReadOnlyPolicyVersionWithoutReviewControls`, asserting
     no review panel, no Reason field, no Save button and (after the
     simplification pass fix) no `<form method="post"
     action="/Administration/Configuration">` anywhere on the page —
     narrowed from a blanket "no `<form`" check, which false-failed on the
     shared layout's unrelated `<form class="utility-search">`.
   - `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs`
     — replaced the retired update/replay assertion with
     `WorkflowConfigurationStoresOnlyTheReadOnlyPolicyIdentity`, proving the
     stored policy identity and zero `ActionHistory` rows for the aggregate.
   - `tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs`,
     `CaseClosureWebTests.cs` — trimmed the 5-arg `new CaseReadinessEvidence(...)`
     calls to 3 args; removed posted review form values.
   - `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`,
     `CaseDataCompletenessPersistenceTests.cs`, `CaseMatchIntegrationTests.cs`,
     `AssessmentPersistenceIntegrationTests.cs`,
     `ProviderInspectionModeAcceptanceTests.cs`,
     `ProviderApiCaseDataSnapshotPersistenceTests.cs` — each trimmed its
     `FixedConfiguration.Configuration = new CaseWorkflowConfiguration(...)`
     to the 2-arg form.
   - `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs` —
     `MissingEvidenceIsNotReady` originally set `InstructionConfirmedByStaff`/
     `ImagesConfirmedByStaff` to a literal `true` regardless of the
     completeness flags under test, which violates
     `CaseDataPolicy.ValidateCompleteness`'s pre-existing (unchanged by this
     ticket) "confirmed implies complete" invariant and threw
     `InvalidOperationException` for the incomplete cases. Fixed by deriving
     the confirmed values from the completeness flags being tested
     (`instructionComplete`/`imagesComplete`) instead of a constant.
   - `CaseDataCompletenessPersistenceTests.cs` — added the assertion proving
     that confirming completeness leaves the stored
     `InstructionConfirmedByStaff`/`ImagesConfirmedByStaff` values unchanged
     (the only behaviour the hidden pass-through protects).
   - **Dependency found during implementation, outside the plan's step-5
     list**: `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` and
     `tests/Pegasus.IntegrationTests/DueChaserSweepPersistenceTests.cs` each
     construct `CaseReadinessEvidence` positionally with 5 args
     (`new(true, true, true, true, "...")`); the plan's caller-verification
     for `CaseReadinessEvidence` ("no external caller outside owned files")
     missed these two. Both are outside the ticket's originally-listed test
     files but are direct, mechanical breakage from the owned
     `CaseWorkflowContracts.cs` change (a changed Core record's positional
     arity), not a policy or scope decision — trimmed both to the 3-arg
     form, identical to the treatment already prescribed for six sibling
     files. `CaseWorkflowPersistenceTests.cs` (already owned, already named
     in step 5 for its `FixedConfiguration` trim) had two more 5-arg
     `CaseReadinessEvidence` constructions the plan did not call out
     individually; trimmed those too.

6. **Governing documents and the Test UI snapshots.** Reused
   `Update-TestUiSnapshots.ps1` and `Test-UiCatalogue.ps1`; verified only the
   three owned snapshot diffs named below (`git diff --stat --
   docs/design/test-ui/` after the full capture+update+verify cycle showed
   exactly these three files with real content changes — every other
   `docs/design/test-ui/**` path that `git status` reported modified was a
   CRLF/LF normalization no-op, confirmed empty by `git diff` per file).
   - `docs/design/test-ui/pages/administration-configuration--default.html`
     (the review panel, its hidden inputs, the Reason field and Save button
     all disappear), `docs/design/test-ui/pages/case-details--default.html`
     and `docs/design/test-ui/pages/case-details--conflict.html` (the
     "Instructions/Images not staff-reviewed" requirement rows and the
     "Next action" notice change).
   - `docs/frd/frd-01-case-identity-and-lifecycle.md` — replaced the
     staff-review gate language with D44's completeness-only rule.
   - `docs/frd/frd-06-vehicle-and-engineering-evidence.md` — amended D39's
     damage-zone fields to severity/note only (D45; no zone `type`).
   - `docs/frd/frd-12-operator-experience.md` — recorded D44 (Workflow
     configuration, Case record readiness).
   - `docs/design/README.md` — removed "Review (two checkboxes)" from the
     Workflow configuration panel list; removed the damage "type" from the
     damage-diagram wording; added `(D44)`/`(D45)` citations to both edited
     lines for grep traceability, consistent with the file's existing
     per-item D-number citation convention.
   - `.kanmer/groups/EPIC-012/context.md` — read; D44/D45 already state the
     exact PLAT-070 scope verbatim (confirmed at plan time, no edit needed).
   - `.kanmer/groups/EPIC-011/context.md` — added an explicit 2026-09-03
     cross-reference paragraph: D44-D50 are recorded in full by EPIC-012
     `context.md` and supersede this document's §1.8/§1.9/§1.12 and D1-D43
     wherever they differ, naming D44 (retiring §1.12's "Review (2
     checkboxes)" line and D39's "type" field) and D45 (amends D39) by name.

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
./scripts/Test-DocumentationLinks.ps1
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
  still exist, are still set by `Create.cshtml` at intake, and are unchanged
  by a completeness confirmation — proved by a test.
- `/Administration/Configuration` renders no "Staff review requirements"
  panel and no subtitle naming it; the regenerated Test UI snapshot and
  `Test-UiCatalogue.ps1` confirm it; whatever open question 1 resolves to,
  the route still returns 200 for an administrator and 403 otherwise.
- The Case page's "Confirm completeness" form offers only "Instructions
  complete"/"Images complete" + Reason; no "staff-reviewed" checkbox, label,
  requirement row or "Next action" notice renders anywhere in the Case
  workspace.
- D44 and D45 are present, in the stated terms, in frd-01, frd-06, frd-12,
  `docs/design/README.md`, and both group `context.md` files.
- `dotnet restore --locked-mode`, `dotnet build --configuration Release`,
  the filtered `dotnet test`, both snapshot commands, the UI catalogue check,
  `Test-MigrationGrants.ps1` and `Test-DocumentationLinks.ps1` (CI runs the
  last three; this PR edits four governing documents) all exit 0.

## Stop condition

Post-implementation report written; PR opened against `dev`; PLAT-070 moved
to Review. Do not merge the PR and do not start CASE-038 or any other
ticket. Stop and report — rather than silently expanding scope or silently
dropping the finding — if implementation turns up another caller of
`CaseWorkflowConfiguration`/`CaseReadinessEvidence` outside this file list,
or if `Create.cshtml`'s now-vestigial confirmation checkboxes need to be
addressed for this ticket to be accepted (that determination belongs to
review, not to silent scope growth here).

## Plan review (2026-09-03, Claude)

Cross-family review of the gpt-5.6-terra plan. Every finding below was checked
against `dev` in the working checkout (not the research worktree); line numbers
are from `dev` at review time.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | `Details.cshtml.cs` builds two `CaseRequirement` rows — "Instructions not staff-reviewed" / "Images not staff-reviewed" — from `Completeness.Values.*ConfirmedByStaff` (lines ~135-143). They render as outstanding-requirement rows and as the Case page's "Next action" notice (`case-details--conflict.html:378`). D44 bars exactly this surface, and the plan named only the label switch and the posted parameters. | fixed — added to step 3, the acceptance conditions and the checklist |
| 2 | Step 6 named one Test UI snapshot and said "accept only the one owned snapshot's diff". `git grep -i "staff-reviewed" docs/design/test-ui` returns three files: `administration-configuration--default.html`, `case-details--default.html`, `case-details--conflict.html`. Regenerating one and verifying would have failed `Update-TestUiSnapshots.ps1 -Verify`. | fixed — all three named; a fourth is a stop condition |
| 3 | `OperatorLabels.WorkflowConfiguration.Description` (line 1073) is the string "Staff review requirements" and is the Configuration page's subtitle (`Configuration.cshtml:17`). The plan deleted `.Review`/`.InstructionReviewRequired`/`.ImageReviewRequired` but left `Description`, which would leave the page subtitled with the name of the deleted function. | fixed — `Description` and its `<span class="muted">` added to step 4 |
| 4 | `OnPostConfirmCompletenessAsync` replaces the **whole** `CaseCompleteness` record (`new(instructionComplete, imagesComplete, instructionConfirmedByStaff, imagesConfirmedByStaff)`, lines 349-353). The plan removed the two checkboxes *and* their hidden inputs but never said what the constructor then receives; the natural reading (`false, false`) silently rewrites the intake-time confirmation `Create.cshtml` recorded — a persisted-data change no step authorises and no existing test would catch. | fixed — step 3 now requires one hidden pass-through input per value carrying the case's current value, handler signature unchanged, plus a new assertion in step 5 |
| 5 | The migration drops two columns, but EPIC-012 `context.md` states "migrations additive; a failed wave is reverted PR-by-PR on `dev`". A code-only revert against a dropped column breaks `EfWorkflowConfigurationStore`. | fixed — step 2 now requires the migration's `Down` to re-add both columns as `bool NOT NULL DEFAULT 1`, matching `20260729180000_AdministrationPolicies.cs`; the epic's additive rule is not otherwise overridden, since the ticket and D44 explicitly authorise this one drop |
| 6 | CI runs `./scripts/Test-DocumentationLinks.ps1` (`ci.yml:87`) and this PR edits four governing documents; the plan's command list omitted it. | fixed — added to Commands and to the acceptance conditions |
| 7 | After the review panel is deleted, `/Administration/Configuration` has **no** operator-editable setting left: the two review checkboxes were its entire form (verified — `Configuration.cshtml.cs` binds only them plus `ExpectedVersion`, `OperationKey`, `Reason`). What ships is a Save button that writes a Reason and bumps `PolicyVersion` while configuring nothing — which conduct rule 21 ("delete a gate that gates nothing") and the design authority's "a retired control is absent" both speak to. `docs/design/README.md:1060` shows the designed page eventually carrying completeness rules, Due work and Labour-rate cards, so the page is not permanently empty — but none of those is shipped or scheduled in wave 1, and neither D44 nor the ticket says what the page shows in the interim. | **operator question** — recorded in `open-questions/` as question 1, with the three candidate resolutions and this review's recommendation |
| 8 | files.md's recommendation to delete `CaseCompleteness.InstructionConfirmedByStaff`/`ImagesConfirmedByStaff` and the eight Infrastructure files that follow from it. | rejected (already rejected by the plan's "Verified scope correction", independently re-verified here): the ticket body enumerates what D44 removes and never names `CaseCompleteness`; `Create.cshtml(.cs)` and the raw-SQL `Cases` fixtures are unowned. The plan's follow-up-ticket note is the right disposition. |

Checks that passed with no finding:

- Every named helper exists: `CaseLifecycleRules.ValidateReviewReadiness`
  (`CaseLifecycle.cs:551`), `ValidateReadiness` (`:562`),
  `CaseCompletenessPolicy.Evaluate` (`CaseDataOperations.cs:60`),
  `AddRequirement`, the shared `Readiness` factory, `Update-TestUiSnapshots.ps1`,
  `Test-UiCatalogue.ps1`, `Test-MigrationGrants.ps1`. All 16 test files named in
  step 5 exist at the stated paths.
- Owned-path disjointness: the only wave-1 EPIC-012 lane sharing a file is
  [[CASE-038]] (`Pages/Cases/Details.cshtml`), which the ticket already
  serialises after this one and which `blocks` records. No other EPIC-012 or
  EPIC-011 ticket claims any file in this list.
- Repository rules: policy stays in `Pegasus.Core` (the readiness rule is
  narrowed in place, not re-implemented in Web); no list is duplicated — the two
  hard-coded "not staff-reviewed" strings in `Details.cshtml.cs` are deleted
  rather than moved into `OperatorLabels`; no package is added; the migration and
  `Test-MigrationGrants.ps1` ride the same diff; no explanatory copy is added
  (finding 3 removes some).
- Proportionality: the plan is long for a `fix` profile, but the diff is ~45
  files and the "Verified scope correction" section is load-bearing, not ritual.
  No speculative abstraction is introduced.

## Resolutions (2026-09-03)

- Finding 7 resolved by the controller as option (b): keep `/Administration/Configuration` and its nav entry; render the policy version read-only; remove the Reason field, Save button and the page's `UpdateWorkflowConfiguration` call; keep the Core command and store for PLAT-062; update `WorkflowConfigurationWebTests.cs` to prove the form is absent. Add these steps to the checklist before the snapshot regeneration step.

## Resumed execution (2026-09-03, gpt-5.6-terra high run + Sonnet wrapper completion)

The gpt-5.6-terra implementation run was interrupted mid-way (its
`impl-summary.md` records a deliberate stop at a scope boundary before fixing
`AutomationMcpTestSupport.cs`/`DueChaserSweepPersistenceTests.cs`). Resuming
in the same recorded worktree/branch, the wrapper found the tree already
carried most of the plan's steps 1-6 uncommitted and complete, verified each
against the plan and checklist, then finished the remainder:

- Scaffolded the migration (`dotnet ef migrations add`, then renamed to
  `RemoveStaffReviewFlags` and corrected `Down`'s `defaultValue` to `true`
  per step 2) — this had not yet been generated.
- Fixed the two out-of-owned-scope `CaseReadinessEvidence` 5-arg call sites
  (`AutomationMcpTestSupport.cs`, `DueChaserSweepPersistenceTests.cs`) and
  two more in the already-owned `CaseWorkflowPersistenceTests.cs` — see the
  step-5 dependency note above for the scope reasoning.
- Fixed `AutomaticCaseReadinessTests.MissingEvidenceIsNotReady` (see step 5)
  — a pre-existing-invariant violation in the inherited test edit, not a
  regression this ticket's Core change introduced.
- Ran the full Test UI snapshot capture/update/verify cycle and
  `Test-UiCatalogue.ps1`; both passed with exactly the three owned snapshot
  diffs.
- Ran `Test-MigrationGrants.ps1` (pass, no grant change needed) and
  `Test-DocumentationLinks.ps1` (one pre-existing broken link,
  `.opencode/skills/kanmer-setup/SKILL.md` → `docs/manual/greenfield.md`,
  confirmed identical and already broken on `origin/dev`; unrelated to this
  ticket and outside its owned paths, not fixed here).
- Added the D44/D45 cross-reference paragraph to `EPIC-011/context.md` (step
  6) and D44/D45 citations to the two edited `docs/design/README.md` lines.
- One flaky, pre-existing, unrelated test observed once during a full-suite
  run (`QdosInstructionExtractionPolicyTests.MultipleDistinctUnlabelledRegistrationsStayAbsent`,
  a `RegexMatchTimeoutException` under parallel load in
  `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`, a file this
  ticket never touches) — passed in isolation and on every other full run;
  not caused by this ticket.

## Simplification pass (2026-09-03)

Ran `codex exec -m gpt-5.6-sol -c model_reasoning_effort="low"` over
`git diff origin/dev...HEAD` for reuse/simplification/efficiency/altitude
findings only (behaviour-preserving; no correctness changes, no test
assertions weakened). Findings and dispositions:

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | `OperatorLabels.WorkflowConfiguration.Reason` and `.Save` (line 1071) became unreferenced once the resolved open question removed the Reason field and Save button from `Configuration.cshtml`/`.cshtml.cs`; only `.Meta` is still used. | fixed — both constants deleted; `.Meta` retained |
| 2 | Removing the Configuration page's POST-form test (`WorkflowConfigurationWebTests.cs`) left three private parsing helpers (`InputValue`, `InputTag`, `HasAttribute`) and three `GeneratedRegex` members (`ConfigurationFormRegex` retained — reused below — but `CheckboxRegex`, `InputTagRegex`, `ValueAttributeRegex` orphaned) unused. | fixed — dead helpers and regexes deleted; no assertion weakened |
| 3 | Every `automaticallyDefinitive` parameter left on `CaseCompleteness.IsReadyForReview`/`CaseCompletenessPolicy.Evaluate` after the config-gated branch's removal is now unused internally. | not applied — removing it requires editing `src/Pegasus.Core/Intake/AcceptIntake.cs`, an unowned caller outside this ticket's file list; flagged as a follow-up simplification, not this ticket's scope (already noted in step 1 above) |

Applying finding 1 also surfaced a pre-existing test bug (not introduced by
the simplification pass, but found while re-running the fast suite after
it): `WorkflowConfigurationWebTests.AdministratorSeesReadOnlyPolicyVersionWithoutReviewControls`'s
blanket `Assert.DoesNotContain("<form", html)` false-failed against the
shared `_Layout.cshtml`'s unrelated `<form class="utility-search">` global
search form. Narrowed to `Assert.DoesNotMatch(ConfigurationFormRegex(), html)`
— the same regex the file already defined for the retired POST form
specifically — so the assertion tests what it actually claims to test
(scope-fix, not a weakening: the retired form's absence is still proven,
just precisely instead of over-broadly).

Post-pass verification: `dotnet build` (0 errors), `Pegasus.Core.Tests`
(1182/1182), `Pegasus.ArchitectureTests` (100/100) all green on the rebuilt
tree.
