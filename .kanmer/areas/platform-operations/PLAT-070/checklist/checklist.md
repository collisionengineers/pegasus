# Checklist — PLAT-070

- [ ] Core: delete `CaseWorkflowConfiguration`'s two `RequireStaff...BeforeEngineerAssignment` flags and `CaseReadinessEvidence`'s two `...ReviewedByStaff` values (`CaseWorkflowContracts.cs`); update `DefaultCaseWorkflowConfiguration.cs` and `WorkflowConfigurationAdministration.cs` to match.
- [ ] Core: `CaseLifecycle.cs` `ValidateReadiness` delegates to `ValidateReviewReadiness`, keeping only the `PolicyKey`/`PolicyVersion` checks of its own.
- [ ] Core: `CaseContracts.cs` `CaseCompleteness.IsReadyForReview` becomes completeness-only; the two `ConfirmedByStaff` properties are NOT deleted.
- [ ] Core: `CaseDataOperations.cs` `CaseCompletenessPolicy.Evaluate` drops the config/confirmation-gated clause; `CaseDataPolicy.ValidateCompleteness` unchanged.
- [ ] Infra: delete the two `WorkflowConfigurationEntity` properties and their seed values (`AdministrationPolicyEntities.cs`, `AdministrationPolicyModelConfiguration.cs`); update `EfWorkflowConfigurationStore.cs` mapping/replay/audit.
- [ ] Infra: scaffold the forward migration + Designer dropping only `WorkflowConfigurations`'s two review columns, with a `Down` that re-adds both as `bool NOT NULL DEFAULT 1`; update `PegasusDbContextModelSnapshot.cs`. No edit to any historical migration or to the `Cases` table.
- [ ] Web: `_ReadinessHiddenFields.cshtml` drops the two `...ReviewedByStaff` hidden inputs.
- [ ] Web: `_CaseWorkflow.cshtml` drops the two staff-reviewed checkboxes from "Confirm completeness" and the two dialog-data entries from "Return to Review".
- [ ] Web: `Details.cshtml`/`.cs` — remove the review-field UI, the two label/value switch cases, and the two `AddRequirement(... ConfirmedByStaff, "Instructions/Images not staff-reviewed", ...)` calls that drive the requirement rows and the "Next action" notice (narrow; frame stays CASE-038's).
- [ ] Web: `OnPostConfirmCompletenessAsync` still receives the two `...ConfirmedByStaff` values, now from hidden pass-through inputs carrying the case's current values — confirming completeness must not rewrite stored confirmation data.
- [ ] Web: `CaseMutationPageModel.cs` shared `Readiness` factory drops the two review parameters/constants.
- [ ] Web: `Workflow.cshtml.cs`, `Closure.cshtml.cs` drop the two review parameters from their handlers (review field handling only).
- [ ] Web: Administration `Configuration.cshtml`/`.cs` — remove the "Staff review requirements" panel, the `Description` subtitle span, its bound properties, and the two `UpdateWorkflowConfigurationRequest` args.
- [ ] Web: `OperatorLabels.cs` — delete `WorkflowConfiguration.Description`, `.Review`, `.InstructionReviewRequired`, `.ImageReviewRequired` (keep `.Reason`, `.Save`, `.Meta`).
- [ ] Tests: update `CaseReviewReadinessTests.cs`, `AssignCaseEngineerTests.cs`, `AutomaticCaseReadinessTests.cs`, `CaseDataOperationsTests.cs`, `ImmediateExternalPublicationTests.cs`, `AdministrationPolicyTests.cs` for the reduced Core contracts.
- [ ] Tests: update `WorkflowConfigurationWebTests.cs`, `AdministrationPolicyPersistenceTests.cs`, `CaseWorkflowWebTests.cs`, `CaseClosureWebTests.cs` for the removed panel/evidence fields.
- [ ] Tests: trim the `FixedConfiguration` constructor in `CaseWorkflowPersistenceTests.cs`, `CaseDataCompletenessPersistenceTests.cs`, `CaseMatchIntegrationTests.cs`, `AssessmentPersistenceIntegrationTests.cs`, `ProviderInspectionModeAcceptanceTests.cs`, `ProviderApiCaseDataSnapshotPersistenceTests.cs` to the 2-arg `CaseWorkflowConfiguration`.
- [ ] Docs: regenerate all three affected Test UI snapshots (`administration-configuration--default.html`, `case-details--default.html`, `case-details--conflict.html`) and accept no fourth.
- [ ] Docs: record D44/D45 in `frd-01`, `frd-06`, `frd-12`, `docs/design/README.md`; cross-reference in `.kanmer/groups/EPIC-011/context.md` (EPIC-012's already states it).
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] `./scripts/Test-MigrationGrants.ps1`
- [ ] `./scripts/Test-DocumentationLinks.ps1`
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `git grep -i "ReviewedByStaff\|RequireStaffImageReview\|RequireStaffInstructionReview\|staff-reviewed"` returns no current-code matches (historical migrations excepted).
- [ ] Confirm `Create.cshtml`/`.cs` still builds and its confirmation checkboxes are untouched (out of scope; do not edit).
- [ ] Open question 1 in `open-questions/` is answered and ticked (or parked) before implementation starts.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: PLAT-070

## Added 2026-09-03 (finding 7, option b)

- [ ] `Pages/Administration/Configuration.cshtml(.cs)`: render the current policy version read-only; remove the Reason field, the Save button and the page's `UpdateWorkflowConfiguration` call; leave the Core command and the store in place for PLAT-062.
- [ ] `WorkflowConfigurationWebTests.cs`: replace the save tests with a test proving the page renders no form and no Save control.
- [ ] Regenerate and verify the `administration-configuration` snapshots after the change.
