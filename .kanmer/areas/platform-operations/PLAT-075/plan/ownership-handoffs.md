# V1 ownership handoffs

Astra assigns these new paths to Stream A for their named A01-A08 caller and
verification scope. They are additions to the frozen manifest, not permission
for unrelated changes. C acknowledged the narrow test/host handoff in the
remote owner note read from kanmer-board f71c43343 on 2026-09-06.

## Explicit narrow existing-file exceptions

- Connect/Authorize.cshtml.cs: A05 grant identity consent/token host composition.
- PollApprovedInboxTests.cs, RetainedMailTests.cs, LocalIntakeAccessTests.cs,
  QdosAllocationRecoveryTests.cs: required A02 generation/test-support hunks.
- AdministrationSearchAccountWebTests.cs: A01 account-action regression tests.
- README.md, workspaces/README.md, .stitch/DESIGN.md and the two named .zcode
  history files: A08 exact documentation-correction register overrides Closed
  classification solely for its explicitly listed correction.
- EfDocumentRequestStore two-line returned Box identity assignment travels in
  C07; A patch preserved, removed from A worktree after C acknowledgement.
- EfAssessmentReportProjectionSource/EvaCaseImageReader Box identity propagation
  travels in B. A patch preserved, removed from A worktree; no B policy authored.
- Calendar utility API is shared G8 b260098a708213083f0d6691638a1fa1bf3e2365.
  The Core utility/test ownership remains A; B/C adopt identical G then fix their
  own direct time-zone call sites. G9 adds the jointly requested cursor primitive.
- Branch-local B DI/test patch is authored in a detached checkout of
  ca6a97c7252ed1edb03afeb4783fcf15a00d8381. It has no PR and is not a branch
  dependency. Only the exact reviewed patch is handed to B for application.

## New Stream A paths

- `NOW.md`
- `src/Pegasus.Core/AiWork/AdministrationAiJobs.cs`
- `src/Pegasus.Core/Operations/ActionLogs.cs`
- `src/Pegasus.Core/Operations/AdministrationHealthMetrics.cs`
- `src/Pegasus.Core/Operations/StaffMailSendEngine.cs`
- `src/Pegasus.Core/Reports/V1ActivityReport.cs`
- `src/Pegasus.Infrastructure/Custody/CachedDocumentContentStore.cs`
- `src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs`
- `src/Pegasus.Infrastructure/Custody/LocalLogicalDocumentVersionReader.cs`
- `src/Pegasus.Infrastructure/Email/GraphStaffMailSender.cs`
- `src/Pegasus.Infrastructure/Persistence/EfActionLogQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/EfAdministrationAiJobQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/EfAdministrationHealthMetricsQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/EfStaffMailSendStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfStaffMailUploadProgress.cs`
- `src/Pegasus.Infrastructure/Persistence/EfV1ActivityReportQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/SqlStaffMailExecutionLock.cs`
- `src/Pegasus.Web/Mcp/AutomationDocumentStreaming.cs`
- `src/Pegasus.Web/Mcp/KeyVaultOAuthCertificateLoader.cs`
- `src/Pegasus.Web/Pages/Administration/ActionLogs.cshtml`
- `src/Pegasus.Web/Pages/Administration/ActionLogs.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/AiJobs.cshtml`
- `src/Pegasus.Web/Pages/Administration/AiJobs.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Health.cshtml`
- `src/Pegasus.Web/Pages/Administration/Health.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Reports.cshtml`
- `src/Pegasus.Web/Pages/Administration/Reports.cshtml.cs`
- `tests/Pegasus.Core.Tests/AiWork/AdministrationAiJobsTests.cs`
- `tests/Pegasus.Core.Tests/Operations/ActionLogsTests.cs`
- `tests/Pegasus.Core.Tests/Operations/AdministrationHealthMetricsTests.cs`
- `tests/Pegasus.Core.Tests/Operations/StaffMailSendTests.cs`
- `tests/Pegasus.Core.Tests/Reports/V1ActivityReportTests.cs`
- `tests/Pegasus.IntegrationTests/AutomationDocumentStreamingTests.cs`
- `tests/Pegasus.IntegrationTests/AutomationMcpOptionsTests.cs`
- `tests/Pegasus.IntegrationTests/AutomationOAuthCertificateLoaderTests.cs`
- `tests/Pegasus.IntegrationTests/CaseArtifactCustodyRecoveryTests.cs`
- `tests/Pegasus.IntegrationTests/DocumentContentCacheTests.cs`
- `tests/Pegasus.IntegrationTests/StaffMailSendPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/V1ActivityReportPersistenceTests.cs`

G9 shared additions (not yet published): Core/CursorPaging.cs,
Web/Mcp/DataProtectionCursorProtector.cs, Core.Tests/CursorPagingTests.cs,
IntegrationTests/DataProtectionCursorTests.cs; existing Web/Program registration
is A-owned. G9 removes the need for A's discarded local AutomationCursorProtector.
B must remove its temporary duplicate declarations/codec when adopting G9.
