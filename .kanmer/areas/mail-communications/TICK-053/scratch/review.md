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

## Independent re-review — PR #469 at `8b300043182ab14e8716323f6fa6f800bc2ba782` (2026-08-20)

### Changes

1. `docs/capabilities.md` records MAIL-11 as locally implemented and narrows UI-10's remaining work.
2. `docs/current-architecture.md` records retained SQL search, the receipt-owned projection, and bounded GET-only Deleted Items reads.
3. `docs/design/README.md` records the operator-approved local MAIL-11 re-entry while leaving deployment and manual visual acceptance separate.
4. `scripts/Invoke-AzureDatabaseBootstrap.ps1` adds Web SELECT and Worker CRUD expectations for `IntakeSearchDocuments`.
5. `src/Pegasus.Core/Intake/DeletedMailSearch.cs` adds the authorised 100-message Deleted Items use case, result states, paging, and source port.
6. `src/Pegasus.Core/Intake/IntakeContracts.cs` adds reader attachment descriptors and receipt-owned search-document contracts.
7. `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` maps canonical reader fragments to root/attachment search documents.
8. `src/Pegasus.Core/Intake/ProcessIntake.cs` places that projection in the existing receipt draft.
9. `src/Pegasus.Core/Intake/RetainedMail.cs` extends retained list/detail contracts with search terms, match locations, and searchability.
10. `src/Pegasus.Infrastructure/DependencyInjection.cs` composes the fallback and production Graph Deleted sources, preserving explicit production registration with TryAdd fallback semantics.
11. `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` adds resolved-folder metadata paging, MIME reads, global newest-first selection, canonical parsing, and timeout/unavailable mapping.
12. `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` exposes attachment descriptors/ordinals from the existing parser.
13. `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` writes/replaces search documents inside the existing receipt transaction.
14. `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` filters before count/paging and maps search evidence/searchability.
15. `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.Designer.cs` is the generated migration model.
16. `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.cs` creates the one child table, index, FK, and Web/Worker grants without backfill.
17. `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` records the current model.
18. `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` maps the receipt child entity and attachment ordinal.
19. `src/Pegasus.Web/Pages/Mail/Index.cshtml` renders retained/Deleted search, match states, bounded warnings, and paging.
20. `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` validates GET search, chooses retained versus Deleted sources, and preserves scope.
21. `src/Pegasus.Web/Pages/Mail/Message.cshtml` preserves search scope on retained detail links/forms.
22. `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` binds and redirects the retained search term.
23. `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` covers Core search validation, authorisation, projection, and paging.
24. `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` updates the committed migration inventory.
25. `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` covers retained search/no-match/invalid-query Web states, but not an executed Deleted search.
26. `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` proves fallback versus production Deleted source resolution.
27. `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` covers exact-folder GETs, fair bounds, approved mailbox listing, timeout, and cancellation.
28. `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` covers SQL body/name/content matching and one exact ordinal fixture.

The 28-file PIR inventory exactly matches both GitHub's file list and `git diff --name-only origin/dev...HEAD`; `git diff --check` is clean.

### Original blocker reconciliation

- **PR-015 — fixed-in-PR.** `TryAddSingleton` preserves the earlier production Graph registration, and `ProductionGraphRegistrationSurvivesTheInfrastructureFallback` proves exactly one production source while default composition proves the unavailable fallback.
- **PR-016 — fixed-in-PR.** The adapter gathers bounded candidates from each selected approved mailbox, globally orders them, caps MIME reads, and tests a newer match in a later mailbox.
- **PR-017 — remains blocking.** The implementation now sources Deleted mailbox choices from `IApprovedIntakeMailboxes`, but the promised Web evidence is absent. The only zero-retained-row test calls `GraphDeletedMailSearchSource.ListMailboxesAsync` directly; no authenticated `/Inbox?folder=deleted_items&search=...` test proves the mailbox is selectable. This is also captured by PR-025.
- **PR-018 — remains blocking.** The new integer ordinal is not proven to be the same occurrence domain as retained display attachments. `LocalEmailDisplayReader` omits an attachment with no filename, while the canonical reader can infer a name and advances its descriptor ordinal, shifting later attachments. In addition, retained `Message.cshtml` never renders `RetainedMailAttachment.IsSearchable`; only Deleted inline details render searchability. The PIR claim that retained detail discloses exact per-attachment searchability is therefore false.
- **PR-019 — fixed-in-PR.** The page renders explicit retained no-match and overlong-input states; blank input is handled as a supported GET.
- **PR-020 — fixed-in-PR.** A provider `TaskCanceledException` maps to unavailable only when the caller token was not cancelled; genuine cancellation propagates and both cases are tested.
- **PR-021 — fixed-in-PR.** The design/capability owners record the narrow operator-approved local introduction and do not claim deployment, new permission, mailbox mutation, or manual visual acceptance.
- **PR-022 — fixed-in-PR.** The final report enumerates all 28 reviewed files with rationales and keeps the LocalDB/CI qualification.

### Additional comments and disposition

- **Blocking — retained root projection can produce an unlabeled result. Filed as [[PR-024]].** The SQL admission predicate searches every `IntakeSearchDocuments.Text`, including the root body, while `AddSearchMatchesAsync` labels projection matches only when `AttachmentFileName != null`. Message-body labeling comes from the separate retained display body. Those bodies differ for staff forwards, so a result can have an empty `Matches` list, violating FRD-08's visible match-location rule.
- **Blocking — no actual Deleted Items Web caller evidence. Filed as [[PR-025]].** Adapter and registration tests do not satisfy the repository's Web/API caller tier. The authenticated route has no test for matched, unavailable, truncated, paged, or zero-retained-mailbox Deleted results.
- **Blocking — exact attachment repair incomplete. Retained as [[PR-018]].** No duplicate ticket filed: PR-018 already owns exact occurrence correlation and per-attachment disclosure.
- **Non-blocking/pass — production caller.** Production composition now resolves `GraphDeletedMailSearchSource`; Web injects `SearchDeletedMail` and the fallback no longer overrides the explicit registration.
- **Non-blocking/pass — persistence and grants.** One receipt-owned child table is added in the existing migration stream with cascade FK, unique receipt/ordinal index, nullable attachment ordinal, Web SELECT, Worker SELECT/INSERT/UPDATE/DELETE, matching bootstrap matrix, and no backfill.
- **Non-blocking/pass — architecture/simplicity.** The implementation reuses `IIntakeSourceReader`, the existing receipt transaction/store, retained query store, approved mailbox estate, Graph client, and existing /Inbox route. No second parser, repository, database, runtime, generic search framework, mailbox write, or historical reconstruction was added.
- **PIR disposition.** File inventory and most implementation claims are exact, but the retained-detail searchability claim and PR-017's selectable-Web evidence claim are not supported.
- **Simplification disposition.** The four lenses were run and the structural dispositions (reuse, no generic abstraction, bounded work, correct layer ownership) are honest. The remaining findings are correctness/evidence defects, not concealed simplification opportunities.

### CI

At the reviewed head, changes, documentation, reference-data, infrastructure, and unit were green; browser and the three SQL shards were still pending when the needs-changes verdict was recorded. CI state cannot change this verdict because substantive blockers remain.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** It named actual Web caller evidence and visible match locations, but did not explicitly prevent the root search projection from competing with the retained display-body owner.
2. **Did implementation miss anything in the plan?** Yes: exact retained attachment occurrence/disclosure, a one-to-one admission/match-location invariant, and actual Deleted Items Web caller tests.
3. **Did the simplification pass run with honest dispositions?** Yes for simplicity and scope; the failures above are correctness/evidence issues.

### Verdict

**Needs changes.** Do not merge PR #469. Keep TICK-053, PR-017, and PR-018 in Review; PR-024 and PR-025 now block TICK-053 from Backlog. Re-review the shared PR only after these issues are implemented, the PIR is corrected, and replacement CI is fully green.
