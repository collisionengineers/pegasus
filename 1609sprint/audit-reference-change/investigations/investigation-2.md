Read-only assessment completed on `dev` at `5765a527` (same as `origin/dev`). No files were changed, and no builds or tests were run.

**Scope conclusion**

The prefix change is centralized in [`AuditIdentity`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Cases/CaseContracts.cs:106), but it affects every flow that creates, persists, displays, stores, exports, or replays an Audit reference.

| Flow | Current | Prospective |
|---|---|---|
| Standalone Audit from mail/intake or Provider API | `a.` for repairable, `ap.` for total loss | Always `a.` |
| Linked Audit created from Inspection + Audit | `a.` for repairable, `ap.` for total loss | Always `a.` |
| Older same-record `AuditReference` workflow | `a.`/`ap.` based on assessment | Always `a.` |
| Normal Case/PO allocation | `QDOS26001` style base reference | Unchanged |
| Existing persisted references | May contain `ap.` | Should remain immutable |

The assessment assumes “includes inspection + audit” means the linked Audit Case created from an Inspection + Audit record. Under the current model, the original Inspection Case retains its normal `QDOS...` reference and the linked Audit Case receives the derived reference. Changing the original Inspection Case itself to `a.QDOS...` would be a separate identity and sequence design change.

**Direct reference-generation paths**

- [`src/Pegasus.Core/Cases/CaseContracts.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Cases/CaseContracts.cs:106)
  Central `a.`/`ap.` policy. The assessment argument should probably remain because it is still used for evidence, history, reporting, and workflow validation even if it no longer selects the prefix.

- [`src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Lifecycle/CreateAuditCase.cs:96)
  Linked Audit creation, outcome validation, history, replay result, and `AuditIdentity.Create` call. Total-loss gating and assessment persistence should not be removed automatically.

- [`src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:198)
  Standalone Audit allocation. Future total-loss intake will persist `a.<base>` while retaining `AuditAssessment.TotalLoss`, evidence identity, and normal sequence allocation.

- [`src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs:105)
  Persists linked Audit `Reference` and `AuditReference`, creates custody work, writes history, and handles idempotent replay. Both stored fields will change from `ap.<base>` to `a.<base>` for new total-loss linked Audits.

- [`src/Pegasus.Infrastructure/Persistence/EfRecordEngineerFinding.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfRecordEngineerFinding.cs:64)
  Parallel or older same-record workflow that creates a later `AuditReference`. It is still registered and tested, so it must either adopt the new rule or be explicitly retired.

- [`src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs:139)
  Wrong-principal replacement path. New replacement Audits will use `a.`; existing original references must remain unchanged.

- [`src/Pegasus.Infrastructure/Persistence/CaseIdentityAllocator.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/CaseIdentityAllocator.cs:6)
  Base Case/PO sequence allocation. No algorithm or migration change is expected.

- [`src/Pegasus.Web/Pages/Cases/Details.Frame.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Details.Frame.cs:54)
  Computes the proposed Audit reference shown before creation. Total-loss previews will change from `ap.` to `a.`.

- [`src/Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml:352)
  Displays the proposed reference and posts the Create Audit action. The route does not need to change.

**Assessment and intake paths that must remain consistent**

These paths determine or carry the repairable/total-loss assessment even though they do not independently generate the prefix:

- [`src/Pegasus.Core/Intake/ProcessIntake.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Intake/ProcessIntake.cs:651)
- [`src/Pegasus.Core/Intake/Classification/PrincipalMailClassificationPolicy.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Intake/Classification/PrincipalMailClassificationPolicy.cs:261)
- [`src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs:254)
- [`src/Pegasus.Core/Intake/IntakeAllocation.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Intake/IntakeAllocation.cs:56)
- [`src/Pegasus.Core/ProviderApi/ProviderInstruction.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/ProviderApi/ProviderInstruction.cs:23)
- [`src/Pegasus.Core/ProviderApi/ProviderSubmission.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/ProviderApi/ProviderSubmission.cs:344)
- [`src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Lifecycle/CreateAuditCase.cs:96)

The report verdict, evidence, and assessment remain necessary for audit classification, acceptance, history, and reporting. The change only removes their use as a prefix discriminator.

**Persistence, custody, and external output implications**

- [`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:541) already supports both forms through ordinary string columns and unique indexes. No schema migration is required for future-only allocations.
- Existing `ap.` values in `Cases.Reference`, `Cases.AuditReference`, history, workflow events, intake state, and persisted operation payloads must remain readable.
- [`src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs:1092) and [`src/Pegasus.Infrastructure/Custody/LocalCaseCustody.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Custody/LocalCaseCustody.cs:57) use the exact reference in folder names, metadata, and paths. New total-loss folders will use `a.`; existing `ap.` folders should not be renamed.
- [`src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs:35) must continue processing already queued `ap.` work items.
- [`src/Pegasus.Core/Reports/CaseReportGeneration.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Reports/CaseReportGeneration.cs:174), [`CaseReportDeliveryPreparation.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs:48), EVA, AI, notifications, correspondence, exports, and MCP/API responses will emit the new `a.` reference automatically through their existing identity projections.
- [`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:463) searches and sorts references without parsing the prefix. Existing legacy same-record `AuditReference` handling should be reviewed because explicit CaseReference filtering currently targets `Reference`, while global search also includes `AuditReference`.
- [`src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs:490) includes the derived reference in its operation fingerprint. Replaying a previously completed total-loss Create Audit operation after deployment could produce a fingerprint conflict (`ap.` versus newly calculated `a.`). Replay compatibility needs explicit handling.

**Routes and Web consumers**

All active case routes use GUIDs, so the prefix change does not require URL changes:

- `/Cases`
- `/Cases/Create`
- `/Cases/{id:guid}`
- `/Cases/{id:guid}/Assessment`
- `/Cases/{id:guid}/Vehicle`
- `/Cases/{id:guid}/Tasks`
- `/Cases/{id:guid}/Workflow`
- `/Cases/{id:guid}/Custody`
- `/Cases/{id:guid}/Closure`
- `/Cases/{caseId:guid}/Documents/{occurrenceId:guid}/Download`
- `/Cases/{caseId:guid}/Documents/Export`
- `/Cases/{caseId:guid}/Eva/Send`
- `/Cases/{id:guid}/{section}` fragment route in [`src/Pegasus.Web/Program.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Program.cs:338)

Relevant Web/pass-through paths requiring regression review include:

- [`src/Pegasus.Web/Pages/Cases/Details.cshtml`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Details.cshtml:1)
- [`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:651)
- [`src/Pegasus.Web/Pages/Cases/Create.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Create.cshtml.cs:411)
- [`src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs:171)
- [`src/Pegasus.Web/Pages/Cases/Shared/_CaseOverview.cshtml`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Cases/Shared/_CaseOverview.cshtml:230)
- [`src/Pegasus.Web/Pages/Search/Index.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Search/Index.cshtml.cs:158)
- [`src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs:42)
- [`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:148)
- [`src/Pegasus.Web/Pages/Administration/Logs.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Administration/Logs.cshtml.cs:25)
- [`src/Pegasus.Web/Pages/Operations/Index.cshtml.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:295)
- [`src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Pages/ImageIntake/Details.cshtml:129)
- [`src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs:75)
- [`src/Pegasus.Web/Mcp/CaseMcpTools.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Mcp/CaseMcpTools.cs:102)
- [`src/Pegasus.Web/Mcp/IntakeMcpTools.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Mcp/IntakeMcpTools.cs:90)
- [`src/Pegasus.Web/Mcp/MailMcpTools.cs`]( /home/pguser/projects/pegasus/src/Pegasus.Web/Mcp/MailMcpTools.cs:302)

No route currently uses a case reference as a URL path segment. Query/form parameters such as `caseReference` must continue resolving both legacy `ap.` and new `a.` values.

**Canonical documentation requiring review or update**

The outcome-dependent prefix language appears in:

- [`CONTEXT.md`]( /home/pguser/projects/pegasus/CONTEXT.md:44)
- [`docs/frd/frd-01-case-identity-and-lifecycle.md`]( /home/pguser/projects/pegasus/docs/frd/frd-01-case-identity-and-lifecycle.md:11)
- [`docs/frd/frd-02-intake-and-source-identity.md`]( /home/pguser/projects/pegasus/docs/frd/frd-02-intake-and-source-identity.md:181)
- [`docs/frd/frd-05-documents-extraction-and-custody.md`]( /home/pguser/projects/pegasus/docs/frd/frd-05-documents-extraction-and-custody.md:53)
- [`docs/frd/frd-09-provider-and-intermediary-routes.md`]( /home/pguser/projects/pegasus/docs/frd/frd-09-provider-and-intermediary-routes.md:82)
- [`docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`]( /home/pguser/projects/pegasus/docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:47)
- [`docs/adr/0051-linked-audit-case-identity-and-custody.md`]( /home/pguser/projects/pegasus/docs/adr/0051-linked-audit-case-identity-and-custody.md:38)
- [`docs/adr/0002-dotnet-modular-monolith-on-azure.md`]( /home/pguser/projects/pegasus/docs/adr/0002-dotnet-modular-monolith-on-azure.md:20)
- [`docs/capabilities.md`]( /home/pguser/projects/pegasus/docs/capabilities.md:68)
- [`docs/prd/pegasus-product.md`]( /home/pguser/projects/pegasus/docs/prd/pegasus-product.md:175)
- [`docs/principal-profiles/qdos.md`]( /home/pguser/projects/pegasus/docs/principal-profiles/qdos.md:120)
- [`docs/frd/frd-12-operator-experience.md`]( /home/pguser/projects/pegasus/docs/frd/frd-12-operator-experience.md:1)
- [`docs/operations.md`]( /home/pguser/projects/pegasus/docs/operations.md)

Documentation should state that:

- New Audit references always use `a.`
- `AuditAssessment` still records repairable versus total loss
- Existing `ap.` references remain valid historical identities
- Prefix inspection can no longer determine the assessment
- Operators should use the recorded assessment/report outcome
- The original Inspection Case in an Inspection + Audit pair retains its normal Case/PO reference unless that separate behavior is deliberately changed

Historical or frozen material should not be mass rewritten:

- `design/planning-and-old-designs/v26_planning/**`
- `design/planning-and-old-designs/v27_planning/**`
- `docs/docs-review-temp/**`
- [`docs/external-component-documents/eva/drag-and-drop-comparison-test/pegasus-output/`]( /home/pguser/projects/pegasus/docs/external-component-documents/eva/drag-and-drop-comparison-test/pegasus-output/)
- [`reference/rendererref1/DESIGN_SPEC.md`]( /home/pguser/projects/pegasus/reference/rendererref1/DESIGN_SPEC.md:20)
- [`reference/rendererref1/report_data_schema.json`]( /home/pguser/projects/pegasus/reference/rendererref1/report_data_schema.json:5)
- `reference/rendererref1/sample_job_*.json`

The renderer sample jobs currently contain `ap.qdos...` values even where the outcome is repairable, so they appear to be renderer fixtures rather than authoritative reference-generation tests. They need an intentional fixture decision.

**Tests with direct expected-prefix assertions**

These require updates:

- [`tests/Pegasus.Core.Tests/Lifecycle/CreateAuditCaseTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.Core.Tests/Lifecycle/CreateAuditCaseTests.cs:17)
- [`tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs:189)
- [`tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs:453)

The Provider API test should assert both the new `a.` reference and continued `TotalLoss` assessment/evidence persistence.

**Tests requiring additional coverage or review**

- Standalone total-loss allocation and replay:
  - [`tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs:2256)
  - [`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:256)
  - [`tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs:606)
  - [`tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs:112)

- Linked Inspection + Audit total-loss creation:
  - [`tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs:75)
  - [`tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs:62)
  - [`tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs:1428)
  - [`tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs:200)

- Same-record/later AuditReference workflow:
  - [`tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs:560)
  - [`tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs:171)

- Custody naming and recovery:
  - [`tests/Pegasus.IntegrationTests/LocalCaseCustodyAtomicWriteTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/LocalCaseCustodyAtomicWriteTests.cs:107)
  - [`tests/Pegasus.IntegrationTests/ProductionBoxCustodyTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/ProductionBoxCustodyTests.cs:149)
  - [`tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs:2395)
  - [`tests/Pegasus.IntegrationTests/ImageCaseCustodyIntegrationTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/ImageCaseCustodyIntegrationTests.cs:410)

- Replacement and persistence:
  - [`tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs:1025)
  - [`tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs:1)

- Pass-through query, presentation, and output coverage:
  - `tests/Pegasus.IntegrationTests/CaseReport*`
  - `tests/Pegasus.Core.Tests/Reports/*`
  - `tests/Pegasus.IntegrationTests/EvaSubmissionPersistenceTests.cs`
  - `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs`
  - `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
  - `tests/Pegasus.IntegrationTests/WorkCentreWebTests.cs`
  - `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`
  - `tests/Pegasus.IntegrationTests/StaffCorrespondenceWebTests.cs`
  - `tests/Pegasus.IntegrationTests/LogsWebTests.cs`
  - `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`
  - `tests/Pegasus.IntegrationTests/RecentCasesPersistenceTests.cs`
  - `tests/Pegasus.IntegrationTests/ContactDirectoryPersistenceTests.cs`
  - `tests/Pegasus.IntegrationTests/StaffNotificationTests.cs`
  - `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs`
  - `tests/Pegasus.IntegrationTests/UploadOutcomeQueriesTests.cs`
  - `tests/Pegasus.IntegrationTests/CaseCursorQueryPersistenceTests.cs`
  - [`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`]( /home/pguser/projects/pegasus/tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:479)

The exact `ap.` search also found unrelated false positives in `OrganizationDirectoryPersistenceTests.cs` and `Assessment/ValuationTests.cs`; those should not be changed.

**Local and CI verification impact**

The relevant test projects are:

- [`tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj`]( /home/pguser/projects/pegasus/tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj:1)
- [`tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj`]( /home/pguser/projects/pegasus/tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj:1)
- [`tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj`]( /home/pguser/projects/pegasus/tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj:1)
- [`Pegasus.slnx`]( /home/pguser/projects/pegasus/Pegasus.slnx:1)

No test-shard or project-file change is expected. Source/test changes will trigger the existing build, Core, architecture, and SQL integration lanes in [` .github/workflows/ci.yml`]( /home/pguser/projects/pegasus/.github/workflows/ci.yml:20).

Relevant CI/local infrastructure:

- [`.github/actions/dotnet-build/action.yml`]( /home/pguser/projects/pegasus/.github/actions/dotnet-build/action.yml:1)
- [`scripts/Get-CiChangeFlags.ps1`]( /home/pguser/projects/pegasus/scripts/Get-CiChangeFlags.ps1:11)
- [`scripts/Test-CiChangeFlags.ps1`]( /home/pguser/projects/pegasus/scripts/Test-CiChangeFlags.ps1:23)
- [`scripts/Invoke-TestShard.ps1`]( /home/pguser/projects/pegasus/scripts/Invoke-TestShard.ps1:1)
- [`scripts/Test-TestShard.ps1`]( /home/pguser/projects/pegasus/scripts/Test-TestShard.ps1:1)
- [`scripts/Test-DocumentationLinks.ps1`]( /home/pguser/projects/pegasus/scripts/Test-DocumentationLinks.ps1:1)
- [`scripts/Test-TestMarkdownPlacement.ps1`]( /home/pguser/projects/pegasus/scripts/Test-TestMarkdownPlacement.ps1:1)
- [`docs/runbook.md`]( /home/pguser/projects/pegasus/docs/runbook.md:287)
- [`scripts/Initialize-LocalDevelopment.ps1`]( /home/pguser/projects/pegasus/scripts/Initialize-LocalDevelopment.ps1:1)
- [`scripts/Invoke-LocalDevelopment.ps1`]( /home/pguser/projects/pegasus/scripts/Invoke-LocalDevelopment.ps1:1)

The CI routing scripts themselves contain no prefix logic. Documentation changes require the Markdown placement and link checks; source/test changes require the normal build and test lanes. The current production smoke script does not exercise authenticated Audit creation, so release acceptance would need an authenticated total-loss Audit smoke case if this becomes deployable behavior.

The main implementation decisions before coding are whether existing `ap.` references remain immutable, whether the old same-record `AuditReference` workflow remains supported, and whether operators need an explicit assessment indicator because the prefix will no longer identify total-loss cases.