## Independent review — PR #469 at `93c06957` (2026-08-20)

### Changes

- `docs/capabilities.md` and `docs/current-architecture.md`: claim the local MAIL-11 retained/Deleted Items search shape and evidence tier.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1`: adds Web SELECT and Worker SELECT/INSERT/UPDATE/DELETE expectations for `IntakeSearchDocuments`.
- `src/Pegasus.Core/Intake/DeletedMailSearch.cs`: adds the authorized bounded Deleted Items port/use case/result states.
- `src/Pegasus.Core/Intake/IntakeContracts.cs`, `IntakeSearchProjection.cs`, and `ProcessIntake.cs`: expose canonical reader attachment descriptors, derive one mailbox-only search projection, and include it in the existing receipt draft.
- `src/Pegasus.Core/Intake/RetainedMail.cs`: extends the existing workspace scope and retained summary/detail contracts with search terms, match kinds, and attachment searchability.
- `src/Pegasus.Infrastructure/DependencyInjection.cs`: registers the unavailable fallback, Graph client, and production Deleted Items adapter.
- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs`: adds exact-path GET-only folder listing/MIME reads and the approved-estate Deleted Items search adapter.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`: exposes attachment descriptors from the existing canonical parse.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`, `EfRetainedMailboxMessageStore.cs`, and `PegasusDbContext.cs`: atomically write the receipt-owned projection and query retained body/filename/content before SQL count/paging.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.cs`, its designer, and the model snapshot: add the single child table, FK/index, and least-privilege runtime grants.
- `src/Pegasus.Web/Pages/Mail/Index.cshtml(.cs)` and `Message.cshtml(.cs)`: add the authenticated GET caller, Deleted Items disclosure, match labels, and retained return context.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs`: covers Core validation, authorization, paging, and projection contracts.
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, `RetainedMailPersistenceTests.cs`, `ProductionGraphSourceTests.cs`, and `MailWorkspaceWebTests.cs`: cover the committed migration, SQL search, fake-Graph boundary, and Web caller.
- `git diff --check` is clean. At review time changes/documentation/reference-data/infrastructure passed; unit, browser, and three SQL shards were still pending.

### Comments and disposition

#### Blocking — [[PR-015]]

The actual production caller is absent. `AddProductionApprovedMailboxResolver` registers `GraphDeletedMailSearchSource` before `Program.cs` later calls `AddPegasusInfrastructure`; its unconditional unavailable registration wins single-service resolution. Filed as PR-015.

#### Blocking — [[PR-016]]

The all-mailbox 100-message cap is consumed in approved-estate enumeration order, so a busy first mailbox prevents later mailboxes from being searched. This contradicts the documented global “100 newest” scope. Filed as PR-016.

#### Blocking — [[PR-017]]

Mailbox refinements are still derived only from retained Inbox rows. A pollable approved mailbox with no retained row cannot be selected for exact Deleted Items search. Filed as PR-017.

#### Blocking — [[PR-018]]

Attachment searchability is correlated by filename. Duplicate filenames with different readable outcomes are displayed inaccurately rather than by exact attachment identity. Filed as PR-018.

#### Blocking — [[PR-019]]

A zero-result retained search says “No mail has been received,” and a direct overlong query can escape Core validation as a server error. Filed as PR-019.

#### Blocking — [[PR-020]]

`HttpClient.Timeout` cancellation is not mapped to the explicit unavailable state, while genuine caller cancellation still needs to propagate. Filed as PR-020.

#### Blocking — [[PR-021]]

The active Next / 0.3.0 search UI conflicts with the cited design authority's deferred-alpha/no-control and design-review boundary. The plan does not reconcile or evidence that activation. Filed as PR-021.

#### Blocking — [[PR-022]]

The post-implementation report gives layer summaries but does not account for every one of the 26 changed files, so the final report-vs-diff evidence is incomplete. Filed as PR-022.

#### Won't do — historical projection backfill

The automated review suggestion to backfill pre-existing attachment content is rejected. FRD-08, the plan, and the implementation boundary explicitly prohibit backlog reconstruction; existing attachments without the canonical projection must remain honestly unsearchable. No ticket filed for a prohibited backfill.

### Checks that passed review

- The migration stays in the existing stream with one receipt-owned table, cascade FK, unique receipt/ordinal index, and matching bootstrap matrix.
- Runtime grants are appropriately split: Web SELECT only; Worker projection read/write. No Outlook write or broader Graph permission was introduced.
- The implementation reuses `IIntakeSourceReader`, `EfIntakeReceiptStore`, `EfRetainedMailboxMessageStore`, `GraphMailClient`, and the existing Web workspace. It adds neither a duplicate parser/store nor a hidden backfill.
- The post-implementation verification qualification is honest about LocalDB contention and defers clean full-suite authority to CI.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** Yes. It missed the canonical design/activation reconciliation and the need to source exact Deleted Items mailbox refinements from the approved estate.
2. **Did implementation miss anything in the plan?** Yes. Production does not resolve the Graph source; the global bound, unavailable state, exact attachment disclosure, and honest search states do not meet the planned behavior.
3. **Did the simplification pass run with honest dispositions?** Yes. The four lenses and applied behavior-preserving changes are recorded honestly. These review findings are correctness/governance defects, not undisclosed simplification findings.

### Verdict

**Changes requested.** PR #469 must not merge and TICK-053 remains in Review. Resolve [[PR-015]] through [[PR-022]], refresh the report, and obtain a fully green required CI set before independent re-review.
