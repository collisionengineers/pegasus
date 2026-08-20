# Files — MAIL-16 (backfill; all present on origin/main = 2325ed4a ancestor path)

Shares the MAIL-14 pipeline ([[TICK-013]]); auto-match-specific surface:

- `src/Pegasus.Core/Workflow/PollSentEvidence.cs` — `HandleItemAsync` auto-link branch (~L479–524): retain → single-case-identity auto-link → `ReportEvidenceAutoLinked` / `ReportEvidenceRetainedUnlinked` / `Ambiguous`
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260729160000_CaseWorkflowRuntime.cs` — `CaseReportSentEvidence` (CaseId, MailboxIdentity, ImmutableItemIdentity, ConversationIdentity, ReplyChainIdentity, SentAtUtc, LinkedAtUtc, LinkedByKind/SubjectId/RolesJson)
- `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs` — `ExactCaseIdentityAutoLinksRetainedReportEvidence`, `IneligibleExactCaseRetainsReportEvidenceVisibleAndUnlinked`, `AmbiguousCaseIdentitiesRetainOneVisibleUnlinkedReportItem`
- `tests/Pegasus.Core.Tests/Workflow/AutoLinkReportEvidenceTests.cs` — link operation
