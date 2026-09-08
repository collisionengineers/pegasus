# Post-implementation report — INTK-066

## Outcome and current authority

Implemented explicit manual-upload destination confirmation and editable new-Case proposal acceptance/rejection. ManualUpload never silently associates or allocates Case/PO before staff acceptance. Existing Mailbox/Provider automatic intake remains on its existing route. Single/group decisions use shared viable destinations, actual route membership, reviewed versions, reason, staff authorization, lease/concurrency and identical-decision replay. Existing awaiting-image queue and Mail Case search remain wired.

The operator subsequently explicitly ordered removal of the unsupported corpus percentage gate, then ALL snapshot/capture and browser tests locally and in CI, with PR resubmission. These are recorded scope revisions, not attempts to label failing tests PASS. Removed the entire generated catalogue/capture framework, browser tests including cases outside Browser/, CI jobs/step, local UiMode/catalogue path, capture hooks and test-only package references. Retained production Playwright/Chromium for PDF rendering, with local installer/checker bound to Infrastructure output. No replacement browser harness, package, schema or new operational service.

## Workspace and delivery

Recorded worktree: .worktrees/INTK-066. Branch: INTK-066-manual-upload-confirmation. Integration base at execution: 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c on dev. No production operation, deployment, merge or foreign-worktree cleanup authorized/performed. PLAT-046's separate PR711 remains separate.

## Governing requirements and production callers

FRD-02 matching, grouped upload, ManualUpload allocation and confirmation clauses now agree with the operator's accepted workflow. FRD-12 aligns the upload experience and removes the retired automated-browser evidence contract without dropping actual accessibility/interaction requirements. Core IntakeAssociationDestinationPolicy is the one current viability owner, shared by EfIntakeAssociationDestinations reads and EfIntakeMutationStore writes. UploadOutcomeQueries and UploadCaseDecision consume the port; actual UploadStatus/UploadGroupStatus and awaiting-image Cases/Index call the existing mutation/lease owners. Cases/Create reuses existing corrected-draft acceptance and real retained classified Audit evidence. No arbitrary Audit creation, new schema, dual route or hidden fixture auto-acceptance.

## Retained attempts and dispositions

All exact commands, exits and earlier attempts remain in scratch/execution-history-1 through -4 and scratch/execution. Early compile/test failures were fixed in reachable commits, not erased. Browser/capture passes are historical only and are NOT current visual acceptance.

- Full Release build at 75f112a18 passed, zero warnings/errors. Core passed 1,955 with 14 skipped; Architecture passed 116.
- Full non-corpus/nonbrowser Integration at 75f112a18 completed exit1: 1,969 passed and five failed (1,974 total, 54m33s). These were obsolete automatic-manual-allocation expectations in four InstructionDraft cases plus one QdosIntake route expectation. Commit137230c corrected the direct consumers while retaining field/source/hash/custody/conflict/replay assertions. Focused two-class run at137230c passed9/9. This is not a claim that the full aggregate reran green.
- Capture at137230c passed its commands but static inspection found per-file decision controls exposed while a group member was still processing. Commit684ddce fixes the existing compact/readiness condition and strengthens the existing test with both attachable and proposal siblings.
- The subsequent group capture was explicitly cancelled by the operator: browser4/4 had completed, nonbrowser phase INCONCLUSIVE, command exit1, later phases NOT RUN. Owned children self-exited; no foreign process killed. All capture grants revoked. Operator then ordered deletion of the whole snapshot/browser system.
- Removal static diff check initially found an extra blank EOF in ci.yml; fixed before commit6c58bdf. A documentation helper initially patched the shared checkout instead of its assigned worktree; its exact eight own hunks were reapplied to INTK and reversed from shared source, with hash equality to HEAD confirmed and foreign Principal edits preserved. No application data affected.

## Verification boundary and risk

Remaining unit and HTTP/SQL tests establish their selected behavior, not pixel layout, browser interoperability or operator acceptance. Removed tests no longer provide those claims. Production PDF runtime dependency remains, but browser-rendered PDF appearance is not asserted by the remaining automated tests. Historical review artifacts under docs/docs-review-temp and dated operations observations remain evidence, not active commands or consumers.

Independent review and post-merge verification remain required. Run the remaining project/CI checks against the exact merge SHA per current runbook; do not resurrect Browser/capture lanes or run retired scripts. No full new-head aggregate PASS is claimed without the actual result.

## Final removal verification

Final source head: e8bc3fcb47b2b47e405c806d17314cccefc71e26. The verification run started at6c58bdf1cfa5f238d505956e9ab4e59a9eb32035 and normally regenerated the only changed lock before locked restore/build/test. Final commit includes that exact tested lock plus the certificate hint wording, not changed application/test inputs.

- Unlocked restore PASS exit0 (4.65s); locked restore PASS exit0 (2.95s). Only Integration lock changed (+9/-34), no package-version upgrade.
- Release solution build PASS exit0, zero warnings/errors, 1m45.97s.
- Focused Integration filter PASS exit0:80 passed,6 existing environment-dependent Qdos skips,0 failed,86 total,5m22s. Both group-processing Theory cases (False/True) were selected.
- CI change classification, shard partition regression (21 examples), platform script, documentation links (140 files), Markdown placement and git diff checks PASS exit0. Six surviving changed PowerShell scripts parsed with zero errors.
- No Browser-tagged/Playwright.CreateAsync/OfflineBrowserAxe tests, capture hooks or deleted script callers remain in current executable sources. Production Infrastructure Release playwright.ps1 exists without launching Chromium. No browser/capture/installer command ran.
- GitHub effective dev/main rules contain no required status checks for the removed jobs; legacy dev status-check-protection endpoint returned404/not protected. No remote rules changed.
- Exact command/fixture/environment/process evidence is in scratch/execution, under Sole-host removal verification result. Six owned nested-build nodes were identity-validated and stopped after their parents exited; no foreign process touched. An auxiliary read-only PowerShell quoting diagnostic failed and is retained; required commands all passed.

## Changed-file census

All155 baseline-to-final changed paths are listed below. D entries are recoverable in Git. Unrelated immutable corpus, runtime rendering code/container dependencies, schema/grants, provider/mail automation, and production estate were not changed by the removal.

| Change | Path | Purpose |
| --- | --- | --- |
| M | `.agents/skills/razor-pages-ui-implementation/SKILL.md` | Remove the retired snapshot procedure; retain proportional Razor implementation checks. |
| M | `.github/workflows/ci.yml` | Delete browser/capture jobs and catalogue step; retain all non-corpus integration tests without an obsolete Browser exclusion. |
| M | `AGENTS.md` | Remove retired capture/snapshot instructions from unmanaged repository guidance. |
| M | `README.md` | Remove obsolete catalogue entry-point guidance. |
| M | `docs/design/README.md` | Remove catalogue workflow and automated-browser evidence claims; keep actual interaction/accessibility requirements. |
| D | `docs/design/test-ui/catalogue.json` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/index.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/access-denied--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-account-confirm--clear-lease.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-account-confirm--delete.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-account-confirm--disable.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-account-confirm--enable.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-account-confirm--force-logout.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-account-confirm--reset-password.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-accounts--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-accounts--empty.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-action-logs--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-ai-jobs--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-automation--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-automation-activity--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-claim-source-edit--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-claim-sources--empty.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-configuration--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-glass--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-health--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-mailboxes--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-principal-create--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-principal-replace--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-principal-settings--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-principals--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-reports--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-roles--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/administration-valuation-presets--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/case-create--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/case-details--conflict.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/case-details--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/case-details--unavailable.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/case-eva-send--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/cases--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/cases--empty.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/cases--unavailable.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/connector-authorize--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/dashboard--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/error--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/inbox--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/inbox--empty.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/inbox--unavailable.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/inbox-message--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/mail-compose--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/operations--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/operations--empty.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/operations--partial-data.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/password-change--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/queues--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/queues--empty.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/received-details--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/sign-in--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/sign-in--signed-out.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/sign-in--validation.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/status-code--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/triage-details--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/unidentified-details--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload--validation.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-group-status--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-group-status--needs-decision.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-group-status--processing.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-request--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-request--validation.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-status--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-status--needs-decision.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/upload-status--processing.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `docs/design/test-ui/pages/vehicle-images-details--default.html` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| M | `docs/engineering.md` | Remove retired snapshot verification inputs from current policy. |
| M | `docs/frd/frd-02-intake-and-source-identity.md` | Settle the manual-upload confirmation/proposal exception across matching, allocation, image recovery and grouping. |
| M | `docs/frd/frd-12-operator-experience.md` | Align upload interactions with explicit confirmation, editable proposals and no pre-acceptance reference. |
| M | `docs/index.md` | Remove generated-snapshot formatting exception. |
| M | `docs/runbook.md` | Remove snapshot/browser test commands and dual UI modes; retain runtime PDF setup through Infrastructure. |
| M | `scripts/Get-CiChangeFlags.ps1` | Remove retired snapshot/catalogue paths from build classification. |
| M | `scripts/Initialize-LocalDevelopment.ps1` | Install required runtime Chromium from Infrastructure output, not the test project. |
| M | `scripts/Invoke-Doctor.ps1` | Check and repair actual Infrastructure Chromium dependency; remove browser-test-specific guidance. |
| M | `scripts/Invoke-LocalDevelopment.ps1` | Remove Test catalogue mode and UiMode parameter; retain the one real application lifecycle. |
| M | `scripts/Invoke-TestShard.ps1` | Update the documented remaining integration filter. |
| M | `scripts/PegasusPlatform.ps1` | Replace obsolete browser-test wording in the Linux certificate-trust repair hint; command and runtime behavior unchanged. |
| M | `scripts/Test-CiChangeFlags.ps1` | Delete regressions for retired snapshot-path rules, retaining other classifier assertions. |
| D | `scripts/Test-UiCatalogue.ps1` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `scripts/Test-UiModes.ps1` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `scripts/Update-TestUiSnapshots.ps1` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| M | `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs` | Retain manual images pending reasoned association through first processing and recovery. |
| M | `src/Pegasus.Core/Intake/DurableIntake.cs` | Withhold automatic association for ManualUpload without changing Mailbox/Provider automation. |
| M | `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Prevent automatic Case/PO allocation for ManualUpload. |
| A | `src/Pegasus.Core/Intake/IntakeAssociationDestinations.cs` | Own the shared current viability rule and receipt-scoped destination read port. |
| M | `src/Pegasus.Core/Intake/IntakeContracts.cs` | Expose retained association operation identity for exact replay. |
| M | `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the destination adapter for its production callers. |
| A | `src/Pegasus.Infrastructure/Persistence/EfIntakeAssociationDestinations.cs` | Read viable suggested/searched targets from retained receipt data. |
| M | `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Enforce the shared destination rule within the existing mutation transaction. |
| M | `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Project existing persisted operation identity; no new schema. |
| M | `src/Pegasus.Web/Pages/Cases/Create.cshtml` | Present explicit proposal acceptance/cancellation without previewing Case/PO. |
| M | `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs` | Reuse existing corrected-draft acceptance, retained Audit evidence, version chain and truthful replay/error handling. |
| M | `src/Pegasus.Web/Pages/Cases/Index.cshtml` | Bind the existing awaiting-image attachment form to reviewed confirmation inputs. |
| M | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` | Keep the existing queue attachment caller wired and reject grouped manual members on the single-item surface. |
| M | `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Retain existing generic Case search without routing it through upload-specific eligibility. |
| M | `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` | Render viable suggestions, receipt-scoped search, reason and explicit confirmation. |
| M | `src/Pegasus.Web/Pages/UploadConfirmationPageModel.cs` | Bind route-member search and confirmation input/errors to the shared decision owner. |
| M | `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml` | Render one group destination decision with retained reviewed input. |
| M | `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs` | Validate actual group membership, preserve partial completion and identical replay identity. |
| M | `src/Pegasus.Web/Pages/UploadStatus.cshtml` | Render the single upload's current confirmation and retained input. |
| M | `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs` | Bind single-route decisions, redirect grouped members and refuse forged single-member search/POST. |
| M | `src/Pegasus.Web/Presentation/UploadCaseDecision.cs` | Coordinate existing leases/mutations, viable target search, exact replay and truthful per-member partial outcomes. |
| M | `src/Pegasus.Web/Presentation/UploadOutcome.cs` | Project pending manual choices/proposals and settled image/Unidentified outcomes honestly. |
| M | `src/Pegasus.Web/wwwroot/js/site.js` | Keep keyboard search and reviewed Case version; distinguish request failure and invalidate stale results. |
| M | `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` | Align genuine automatic-channel expectations without restoring manual auto-acceptance. |
| M | `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs` | Prove manual image retention, recovery and confirmed completion. |
| M | `tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs` | Prove manual non-allocation and unchanged non-manual allocation. |
| A | `tests/Pegasus.Core.Tests/Intake/IntakeAssociationDestinationTests.cs` | Prove shared source/destination viability. |
| M | `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Use the genuine automatic image channel for automatic reconciliation role evidence. |
| D | `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/InspectionAddressChoiceBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/OuterShellBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/QdosAllocationRecoveryBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/UploadDropzoneBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/UploadRowsBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| M | `tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs` | Prove proposal, retained Audit acceptance, no pre-acceptance identity, validation, permissions and safe replay. |
| M | `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Remove obsolete capture comment; keep functional assertions. |
| M | `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs` | Prove the actual awaiting-image attachment caller and forged/grouped-member refusal. |
| M | `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Explicitly accept fixtures that require allocated Cases while preserving custody assertions. |
| M | `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs` | Align persisted manual pending/confirmed behavior and genuine automatic fixtures. |
| M | `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Align actual manual image decisions and explicit accepted-case fixtures. |
| M | `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Build a genuinely accepted Case and attach image through the real reasoned/versioned form. |
| M | `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` | Retain exact extraction/hash/custody/replay evidence and assert no automatic manual Case/link/sequence/event. |
| M | `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | Remove capture startup filter and image-fetch side effects; preserve real HTTP/test authentication helpers. |
| M | `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Remove the browser-driven axe test while retaining genuine HTTP/extraction cases. |
| M | `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs` | Remove obsolete capture comments; preserve credential and HTTP tests. |
| M | `tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj` | Remove direct browser-test/axe dependencies; runtime Playwright remains transitively through Infrastructure. |
| D | `tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs` | Delete the operator-rejected unsupported percentage gate; retain substantive extraction/routing/classification tests. |
| M | `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs` | Assert current manual proposal/search and unallocated receipt while preserving pending/duplicate behavior. |
| M | `tests/Pegasus.IntegrationTests/ReadinessEndpointTests.cs` | Delete browser readiness/axe helper and capture injection; retain SQL/HTTP composition and readiness checks. |
| M | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | Remove claims of retired browser-renderer coverage; retain real upstream HTTP/report logic. |
| M | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Remove browser-dependent render cases/helpers; retain non-rendering resource/composition checks. |
| M | `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs` | Remove obsolete catalogue comment; retain account/role assertions. |
| D | `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/TestUiResponseCapture.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| D | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | Delete the operator-retired browser/capture test, tool or generated catalogue asset. |
| M | `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Use the existing two-step reviewed attachment contract in the affected awaiting-image tests. |
| M | `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Prove real single/group destination decisions, permissions, membership, versions, reasons and replay. |
| M | `tests/Pegasus.IntegrationTests/UploadOutcomeQueriesTests.cs` | Prove unique/ambiguous viable choices and honest image/Unidentified/proposal state precedence. |
| M | `tests/Pegasus.IntegrationTests/packages.lock.json` | Regenerate the lock for explicitly removed browser-test packages; preserve required runtime versions. |

## Reachable implementation commits

- `e8bc3fcb47b2b47e405c806d17314cccefc71e26`
- `6c58bdf1cfa5f238d505956e9ab4e59a9eb32035`
- `684ddce42c6a8535d603adb54354c9b3c2bca6b5`
- `137230ca4f9b5115e1176f387fd360dac7370d65`
- `75f112a18ef7c228a044772686d8cbb403a4825d`
- `e843b5ee523aaf286541b20934dcf3e6d46fead1`
- `500b86a9b21adbd7a8fe56a65ddd52782630ce21`
- `cca2c76cb9d4acc19e68a1a776719d5dc701f8d3`
- `cc826889407b97dc2d951219c70b59e619de70f2`
- `4b3329675f48faede428a9c97212d1a610584136`
- `c57d8487cd343321a07abb68c161bd7d9a00aa27`
- `26bf5d00206014f58adf8149abe39084fd52b3f4`
- `4a17becc09584696531611b4dc39d19521f489b3`
- `da6ff8e16815100e42da65e60df3e45a3cced2d2`

## Final hint verification and handoff

At exact clean final HEAD e8bc3fcb47b2b47e405c806d17314cccefc71e26, Test-PegasusPlatform.ps1 PASS exit0, PowerShell Parser for PegasusPlatform.ps1 PASS zero errors, git diff --check PASS exit0. Both host records IDLE/unassigned and zero host processes. Application/build/HTTP evidence above used the same final lock and unchanged compiled inputs; only this verified text hint differed.

The handoff is a draft-then-ready PR to dev after the Review gate is satisfied. Independent review and post-merge proof are not author claims; no merge/deployment or next ticket is performed.
