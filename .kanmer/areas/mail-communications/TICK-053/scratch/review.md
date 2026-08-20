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

## Final independent re-review — PR #469 at `c0fa9a9905f2808ec1e2eb03e42dbe29cfde7ae4` (2026-08-20)

### Changes

1. `docs/capabilities.md` records MAIL-11/UI-10 local implementation and separate deployment evidence.
2. `docs/current-architecture.md` records the local retained/Deleted search shape without a deployed claim.
3. `docs/design/README.md` records the approved local MAIL-11 re-entry and remaining release evidence.
4. `scripts/Invoke-AzureDatabaseBootstrap.ps1` adds the projection table to the existing Web/Worker permission matrix.
5. `src/Pegasus.Core/Intake/DeletedMailSearch.cs` owns authorization, the 100-message bound, paging and unavailable state.
6. `src/Pegasus.Core/Intake/IntakeContracts.cs` adds canonical attachment descriptors and receipt search documents.
7. `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` projects existing reader output into root/attachment search documents.
8. `src/Pegasus.Core/Intake/ProcessIntake.cs` writes the mailbox-only projection through the existing receipt draft.
9. `src/Pegasus.Core/Intake/RetainedMail.cs` extends the existing list/detail contracts with search and match/searchability evidence.
10. `src/Pegasus.Infrastructure/DependencyInjection.cs` composes the fallback without overriding the production Graph source.
11. `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` adds bounded approved-estate Deleted metadata/MIME reads, canonical parsing and recoverable HTTP/timeout handling.
12. `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs` retains nameless display attachment occurrences with deterministic labels.
13. `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` exposes canonical attachment descriptors and ordinals.
14. `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` stores/replaces search documents in the existing transaction.
15. `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` filters before count/paging and maps match/searchability evidence.
16. The migration designer records the generated model for the new child table.
17. The migration creates `IntakeSearchDocuments`, its FK/index and runtime grants without backfill.
18. The model snapshot records the same current schema.
19. `PegasusDbContext.cs` maps the receipt-owned search-document entity.
20. `Index.cshtml` renders GET search, Deleted disclosure, labels, bounds, states and pagination.
21. `Index.cshtml.cs` chooses retained versus Deleted sources and validates/preserves scope.
22. `Message.cshtml` preserves scope and renders retained attachment searchability.
23. `Message.cshtml.cs` carries search context but still checks outside-list state only by mailbox/folder.
24. `OperatorLabels.cs` remains the one operator-wording owner for searchability.
25. Core tests cover request validation, authorization, bounding and synthetic occurrence projection.
26. Migration integration evidence includes the committed schema/table.
27. Web tests cover retained states and the authenticated zero-retained-row Deleted caller with match, truncation, 25/1 paging and unavailable rendering.
28. Production composition tests prove the real Graph source wins and fallback profiles remain unavailable.
29. Graph tests cover exact metadata folder reads, fair cross-mailbox selection, bounds, approved mailbox listing, timeout and cancellation.
30. Persistence tests cover SQL body/name/content matching, root-only rejection, detail disclosure, and nameless parser ordering.

The TICK-053 PIR now enumerates exactly these 30 files and `git diff --check origin/dev...HEAD` is clean.

### Comments and disposition

- **PR-015 — fixed-in-PR.** `TryAddSingleton` plus production/default composition evidence proves the real Graph source is reachable.
- **PR-016 — fixed-in-PR.** Candidates are bounded per mailbox, globally ordered, and MIME reads are capped after fair selection.
- **PR-017 — fixed-in-PR.** The authenticated zero-retained-row Deleted caller now renders the approved mailbox and passes its exact ID/term/100 bound.
- **PR-018 — blocking retained.** Nameless attachments are preserved and retained detail now discloses searchability, but attached `TextPart` entities still return before canonical descriptor creation. A displayed `text/plain` attachment before a searchable PDF shifts the PDF's canonical ordinal and can label the wrong displayed attachment. Existing tests cover nameless `MimePart`, not this current path.
- **PR-019 — fixed-in-PR.** Blank, overlong and no-match states are supported and honest.
- **PR-020 — fixed-in-PR.** HTTP timeout maps to unavailable without swallowing caller cancellation.
- **PR-021 — fixed-in-PR.** Design/capability authority records local activation without claiming deployment, permissions or mailbox mutation.
- **PR-022 — fixed-in-PR.** The corrected 30-file PIR inventory exactly matches the final diff.
- **PR-024 — blocking retained.** Root projection admission is restricted correctly, but retained body admission still searches raw `BodyPlainText` while detail applies `StaffForwardBodyCleaner`. Wrapper text or removed `cid:` content can therefore produce a “Message body” match that is absent from the body operators see.
- **PR-025 — fixed-in-PR.** The authenticated Web route proves the requested Deleted source and rendered states.
- **Blocking — [[PR-029]].** Thread links preserve the search term, but `ReloadAsync` omits the active search predicate from `OutsideListScope`; a nonmatching thread member is not marked no-longer-in-view.
- **Blocking — [[PR-030]].** MIME is fetched through a mailbox-global route after folder enumeration. A concurrent move can return content no longer in the resolved Deleted Items folder.
- **Blocking — [[PR-031]].** Azure credential/token-acquisition failures escape the Deleted external-boundary catch policy as a 500 instead of the existing unavailable state.
- **Won't-do — historical backfill.** The automated backfill suggestion conflicts with FRD-08 and the accepted no-reconstruction boundary. Existing mail without a projection must remain honestly unsearchable; no backfill ticket is filed.
- **Non-blocking/pass — one-owner and simplicity.** The diff reuses the canonical reader, existing receipt transaction, retained query store, approved estate, Graph client and /Inbox route. It adds no second parser/store, generic search framework, mailbox write or hidden backfill. The two dated four-lens passes name applied fixes and honest bounded work.
- **Release coordination.** The PR also carries the repository owner's Release-14 hold. No merge is attempted.

### CI

Replacement CI at the reviewed head has green changes, documentation, local-development-scripts, reference-data, infrastructure and unit jobs. Browser and all three SQL shards were still running when this substantive needs-changes verdict was recorded; their result cannot make the current head mergeable by review.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** Yes: binding MIME content to the folder at read time, credential failure mapping, search-aware thread membership, and using the displayed normalized retained body as the search owner.
2. **Did implementation miss anything in the plan?** Yes: exact attachment occurrence for attached text parts, fully visible retained body match locations, exact Deleted folder membership at MIME read, and all unavailable paths.
3. **Did the simplification pass run with honest dispositions?** Yes. The remaining issues are correctness/evidence defects, not concealed abstraction or simplification findings.

### Verdict

**Needs changes.** Do not merge PR #469. TICK-053 and every resolved shared-PR blocker remain in Review; PR-018 and PR-024 are retained as active blockers, and PR-029 through PR-031 newly block TICK-053. Re-review only after those five blockers land, the PIR/file inventory is refreshed, replacement CI is fully green, and the Release-14 merge hold is cleared.

## Independent final review — PR #469 at `7932d683782669e112f3d996c6914323e8ba72d4` (2026-08-20)

### Changes

1. `docs/capabilities.md` records MAIL-11/UI-10 local implementation while retaining deployment and live-evidence qualifications.
2. `docs/current-architecture.md` records the local retained/Deleted search composition and runtime grants.
3. `docs/design/README.md` records the operator-approved local MAIL-11 re-entry and separate release evidence.
4. `scripts/Invoke-AzureDatabaseBootstrap.ps1` adds the projection-table permission expectation.
5. `src/Pegasus.Core/Intake/DeletedMailSearch.cs` adds the authorized 100-message Deleted search use case, page contract, mailbox source and unavailable fallback.
6. `src/Pegasus.Core/Intake/IntakeContracts.cs` adds canonical attachment descriptors and receipt-owned search documents.
7. `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` derives root/attachment search documents from the existing reader result and route evidence.
8. `src/Pegasus.Core/Intake/ProcessIntake.cs` sends that projection through the existing receipt draft.
9. `src/Pegasus.Core/Intake/RetainedMail.cs` extends existing list/detail contracts with normalized search and exact match/searchability evidence.
10. `src/Pegasus.Infrastructure/DependencyInjection.cs` composes the unavailable default and production Graph source without overriding the latter.
11. `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` adds bounded approved-estate Deleted metadata/MIME reads, exact-folder MIME routing and recoverable timeout/authentication mapping.
12. `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs` preserves nameless displayed attachment occurrences.
13. `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` emits canonical attachment descriptors/ordinals, including attached text parts.
14. `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` inserts/replaces search documents within the existing receipt transaction.
15. `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` applies SQL-first search and maps visible matches/detail searchability.
16. `20260820100724_RetainedMailSearchDocuments.Designer.cs` is the generated migration model.
17. `20260820100724_RetainedMailSearchDocuments.cs` creates the receipt-owned table/index/FK and runtime grants without backfill.
18. `PegasusDbContextModelSnapshot.cs` records that schema.
19. `PegasusDbContext.cs` maps the receipt-owned child entity.
20. `Index.cshtml` renders GET search, match locations, Deleted disclosure and honest bounded/error states.
21. `Index.cshtml.cs` selects retained versus Deleted queries and preserves query scope.
22. `Message.cshtml` preserves search scope and renders retained attachment searchability.
23. `Message.cshtml.cs` carries search into detail/thread membership and correction reload.
24. `OperatorLabels.cs` remains the single operator wording owner for searchability.
25. Core tests cover validation, authorization, bounding and projection normalization/identity.
26. Migration integration evidence covers the new committed migration/table.
27. Web tests cover retained states, authenticated Deleted caller behavior, search-aware thread membership and credential unavailability.
28. Composition tests prove the production source wins and fallback profiles remain unavailable.
29. Graph tests cover exact folder paths, fair cross-mailbox bounds, timeout/cancellation and concurrent moves.
30. Persistence tests cover SQL matches, normalized visible body equality and known attachment-ordinal cases.

The PIR's corrected 30-file inventory matches `git diff --name-only origin/dev...HEAD`; `git diff --check` is clean.

### Comments and disposition

- **PR-015, PR-016, PR-017, PR-019, PR-020, PR-021, PR-022, PR-024, PR-025 and PR-029 through PR-031 — fixed-in-PR.** The final code and focused evidence implement their stated cases without a second parser/store, mailbox mutation, backfill or permission expansion.
- **PR-018 — still incomplete.** Nameless and attached-text cases are fixed, but an explicitly attached image carrying a Content-ID is still omitted from the canonical descriptor sequence while MimeKit keeps it in the displayed attachment sequence. The fresh exact case is filed as [[PR-034]].
- **Blocking — [[PR-033]].** Successful-but-malformed or scope-invalid Graph responses throw `JsonException`, `InvalidDataException` or `UnauthorizedAccessException` outside the Deleted-source catch policy, returning 500 rather than the established unavailable state.
- **Blocking — [[PR-034]].** Content-ID alone currently makes an explicitly attached image inline, shifting every later canonical ordinal and allowing match/searchability attribution to the wrong displayed attachment.
- **Blocking — [[PR-035]].** An invalid classification-correction POST carrying whitespace-only or overlong search context calls `ReloadAsync`, whose new search validation exception is outside the handler catches and becomes a 500.
- **Blocking/security — [[PR-036]].** The Worker is granted UPDATE on `IntakeSearchDocuments`, but the implemented writer selects/deletes/inserts projection rows and has no UPDATE caller. The PIR/current-state least-privilege claim is therefore too broad.
- **Won't-do — historical backfill.** FRD-08 expressly prohibits reconstruction; existing receipts without a projection remain honestly unsearchable.
- **Non-blocking/pass — simplicity.** The implementation reuses the existing Core retained-mail port, canonical intake reader, receipt transaction/store, approved mailbox estate, Graph client and `/Inbox` pages. The dated four-lens passes are honest. The remaining defects need narrow fixes to existing code/tests, not new abstractions.
- **Non-blocking/pass — governing scope.** FRD-08 and the accepted design re-entry authorize this local read-only implementation. No deployment, Graph permission change, mailbox write or live evidence is claimed. The release-14 hold was explicitly cleared in the PR conversation.

### CI

Replacement CI run `32370485614` at the reviewed head has green changes, documentation, local-development-scripts, reference-data, infrastructure, unit, browser, and SQL shards 1 and 3. SQL shard 2 was still running when this needs-changes verdict was recorded. A later green result cannot make this head reviewable because substantive blockers remain.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** Yes: malformed provider-response mapping, Content-ID versus explicit attachment precedence, invalid search context on correction reload, and exact least-privilege grants.
2. **Did implementation miss anything in the plan?** Yes: the stated honest unavailable state and exact attachment identity are incomplete for the cases above; the permission claim exceeds the actual caller.
3. **Did the simplification pass run with honest dispositions?** Yes. The unresolved findings are correctness/security boundary defects and can be fixed narrowly without adding a framework.

### Verdict

**Needs changes.** Do not merge PR #469. Keep TICK-053 and all shared-PR blocker tickets in Review; [[PR-033]] through [[PR-036]] now block TICK-053 from Backlog. Re-review only after those four tickets land, PR-018's exact-identity claim is genuinely complete, the PIR remains exact, and full replacement CI is green.

## Independent re-review — PR #469 at `eaf2f9f4eac577242ed301dd917f0682d4a77729` (2026-08-20)

### Changes

1. `docs/capabilities.md` records the activated local MAIL-11 search capability and retains deployment/live-evidence qualifications.
2. `docs/current-architecture.md` records the receipt-owned search projection, bounded Deleted Items reader, and exact Web/Worker projection grants.
3. `docs/design/README.md` records the narrow operator-approved MAIL-11 re-entry while keeping deployment/manual visual acceptance separate.
4. `scripts/Invoke-AzureDatabaseBootstrap.ps1` adds the projection table to the existing exact runtime-permission census.
5. `src/Pegasus.Core/Intake/DeletedMailSearch.cs` owns staff authorization, the fixed 100-message request bound, ordering/paging, and explicit unavailable state.
6. `src/Pegasus.Core/Intake/IntakeContracts.cs` carries canonical attachment occurrences and one receipt-owned search-document contract.
7. `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` derives normalized visible root and per-attachment documents from the existing canonical reader result and route evidence.
8. `src/Pegasus.Core/Intake/ProcessIntake.cs` supplies that projection only for mailbox receipts through the existing receipt draft.
9. `src/Pegasus.Core/Intake/RetainedMail.cs` extends existing list/detail contracts with normalized search context, match locations, and attachment searchability while preserving TICK-047 folder recommendation.
10. `src/Pegasus.Infrastructure/DependencyInjection.cs` composes the unavailable default and production Graph source with TryAdd fallback semantics.
11. `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` adds exact-folder GET-only Deleted metadata/MIME reads, fair global newest selection, canonical parsing, and established unavailable mappings.
12. `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs` preserves nameless displayed attachment occurrences.
13. `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` emits canonical attachment descriptors, including attached text and explicitly attached Content-ID images.
14. `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` inserts/replaces search documents in the existing receipt transaction.
15. `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` applies retained search before SQL count/paging and maps exact visible matches/detail searchability.
16. `20260820100724_RetainedMailSearchDocuments.Designer.cs` is the generated model for the single new child table.
17. `20260820100724_RetainedMailSearchDocuments.cs` creates that table/FK/index without backfill and grants Web SELECT plus Worker SELECT/INSERT/DELETE.
18. `PegasusDbContextModelSnapshot.cs` records the same current schema.
19. `PegasusDbContext.cs` maps the receipt-owned child entity/navigation.
20. `Index.cshtml` renders GET search, exact match locations, Deleted result disclosure, bounds, unavailable/no-match states, and pagination.
21. `Index.cshtml.cs` selects retained versus Deleted owners, validates query state, and preserves scope/refresh context.
22. `Message.cshtml` preserves search/list context and renders retained attachment searchability.
23. `Message.cshtml.cs` carries normalized search into detail/thread membership and supported correction reload.
24. `OperatorLabels.cs` remains the single operator-facing owner for searchability wording.
25. `RetainedMailTests.cs` proves Core validation, authorization, projection normalization, bounding, and coexistence with the merged MAIL-05 recommendation.
26. `AzureSqlRuntimeRoleMigrationTests.cs` proves exact Web and Worker projection verbs, including absent UPDATE.
27. `IntakePersistenceIntegrationTests.cs` includes the new committed migration/table in repository schema evidence.
28. `MailWorkspaceWebTests.cs` proves authenticated retained and Deleted caller states, context preservation, thread membership, invalid correction search, and credential unavailability.
29. `ProductionCompositionTests.cs` proves production Graph composition wins while unconfigured profiles retain the unavailable fallback.
30. `ProductionGraphSourceTests.cs` proves exact-folder GETs, fair bounds, approved mailbox listing, failure mapping, cancellation, and concurrent moves.
31. `RetainedMailPersistenceTests.cs` proves SQL-first body/name/content search, normalized visible body equality, and exact attachment occurrence/searchability.

The PIR's 31-file inventory exactly matches `gh pr diff 469 --name-only` and `git diff --name-only origin/dev...HEAD`. `git diff --check` is clean. The head has current `origin/dev` commit `a1775841` as its second parent; the resolved Core path applies MAIL-11 search context before TICK-047's existing recommendation and both owning test fakes remain.

### Prior blocker reconciliation

- **PR-015 — fixed-in-PR.** Explicit production Graph composition survives the TryAdd unavailable fallback.
- **PR-016 — fixed-in-PR.** Bounded candidates are collected per selected mailbox, globally ordered newest-first, and MIME reads are capped after selection.
- **PR-017 — fixed-in-PR.** Deleted mailbox refinements come from the approved estate and the authenticated caller proves a zero-retained-row mailbox.
- **PR-018 / PR-034 — fixed-in-PR.** Nameless, attached-text, and explicitly attached Content-ID occurrences remain in the canonical/display ordinal domain; retained detail discloses per-occurrence searchability.
- **PR-019 — fixed-in-PR.** Blank, overlong, and no-match retained searches render supported, honest states.
- **PR-020 — fixed-in-PR.** Provider timeout maps to unavailable without swallowing caller cancellation.
- **PR-021 — fixed-in-PR.** Canonical design/capability owners record local activation without claiming deployment, permission changes, mailbox writes, or manual visual acceptance.
- **PR-022 — fixed-in-PR.** The final 31-file PIR inventory is exact.
- **PR-024 — fixed-in-PR.** One route-aware normalized receipt root owns retained body admission, match evidence, and visible detail; no backfill was introduced.
- **PR-025 — fixed-in-PR.** The authenticated Web route proves approved mailbox selection, exact request bound, matches, truncation, paging, and unavailable rendering.
- **PR-029 — fixed-in-PR.** Detail reuses the active term and marks nonmatching thread members outside the originating view.
- **PR-030 — fixed-in-PR.** Deleted MIME is fetched through the resolved folder path, so a concurrent move becomes unavailable.
- **PR-031 — fixed-in-PR.** Azure token-acquisition failure maps to unavailable while caller cancellation propagates.
- **PR-033 — incomplete due to the remaining shape case filed as [[PR-037]].** The established catch now handles malformed JSON, missing item identities/time, foreign folders, and escaped absolute page links, but the Graph client does not normalize all malformed successful response shapes.
- **PR-035 — fixed-in-PR.** Invalid correction POST search context returns the established supported response without a history write.
- **PR-036 — fixed-in-PR.** Worker UPDATE is removed from migration/bootstrap/current-state claims and exact migrated permission evidence proves its absence.

### Comments and disposition

- **Blocking — [[PR-037]].** `ReadFolderMessagesAsync` uses `root.GetProperty("value").EnumerateArray()` and direct absolute-`Uri` construction. A successful response with missing/non-array `value` or an invalid/non-absolute `@odata.nextLink` throws `KeyNotFoundException`, `InvalidOperationException`, or `UriFormatException`, outside the Deleted-source unavailable policy. A non-object successful folder-resolution root has the same `InvalidOperationException` path through `RequiredString`. The authenticated route can therefore still return 500 for a malformed provider response despite the FRD/PIR fail-closed claim. Filed as PR-037; fix should validate in the existing Graph client and translate to its existing `InvalidDataException`, not broaden the outer catch to swallow application errors.
- **Won't-do — historical projection backfill.** FRD-08 explicitly prohibits backlog reconstruction. Existing rows without the projection remain honestly unsearchable; no backfill ticket is warranted.
- **Pass — governing scope.** FRD-08 and the adopted design re-entry authorize this local read-only slice. No deployment, Graph permission change, mailbox mutation, or live tenant evidence is claimed.
- **Pass — simplicity and least privilege.** The diff reuses the existing Core retained-mail port, canonical intake reader, receipt transaction/store, approved estate, Graph client, and `/Inbox` pages. It adds one child projection and one narrow external read source, not a generic search/action framework, second parser/store, runtime, or backfill. The four dated simplification passes have concrete applied dispositions. Web receives SELECT only; Worker receives only the SELECT/DELETE/INSERT operations used by replacement writes.

### CI

Replacement CI at exact head `eaf2f9f4` currently has green changes, documentation, local-development-scripts, reference-data, infrastructure, and unit jobs. Browser and SQL shards 1–3 remain in progress. The substantive PR-037 blocker makes the head non-mergeable by review regardless of those pending results, so no merge is attempted.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** Yes: it required an honest unavailable state for provider failures but did not enumerate malformed successful Graph page envelopes/URI construction.
2. **Did implementation miss anything in the plan?** Yes: the invalid-response policy remains incomplete for missing/wrong-shaped page collections and invalid next-link URI strings.
3. **Did the simplification pass run with honest dispositions?** Yes. Reuse, one-owner, bounded-work, altitude, and least-privilege claims hold; PR-037 is a narrow correctness gap, not an undisclosed abstraction or scope issue.

### Verdict

**Needs changes.** Do not merge PR #469. Keep TICK-053 and every shared-PR blocker in Review; [[PR-037]] now blocks TICK-053. Re-review after that narrow validation fix lands, the PIR remains exact, and replacement CI is fully green.

## Independent re-review — PASS — 2026-08-20

**PR:** #469  
**Reviewed head:** `6aaf2418c30defc1fb21111a10b954e70f74eea3`  
**Base:** `dev`

### Scope and reconciliation

- Re-read the full TICK-053 ticket documents, group constraints, blocker documents, plan, checklist and PIR, including [[PR-037]].
- Reconciled all prior review blockers [[PR-015]]–[[PR-022]], [[PR-024]], [[PR-025]], and [[PR-029]]–[[PR-037]]. Their planned corrections are present at this exact head.
- [[PR-037]] closes the remaining malformed-Graph-envelope gap at the existing Graph client boundary: non-object roots, missing/non-array `value`, and invalid/non-absolute next links become the existing `InvalidDataException` path and therefore the existing unavailable state. Focused Graph and authenticated Web tests cover those cases without broad exception swallowing.
- The PIR inventory matches the branch exactly: 31 changed files relative to `origin/dev`; `git diff --check origin/dev...HEAD` is clean.
- The merged MAIL-05/dev behavior coexists with MAIL-11: search normalization/query remains upstream of folder recommendation and both existing fakes/conventions are preserved.
- Governing-doc alignment passes against FRD-08 and the canonical design/capability owners. The implementation remains GET-only at the mailbox boundary, validates exact approved mailbox/folder scopes, preserves cancellation and fixed bounds, and keeps SQL privileges at Web SELECT and Worker SELECT/INSERT/DELETE.
- Simplicity passes: existing parsing sites, `InvalidDataException`, URI validation, authenticated host, and HTTP fakes are reused. No generic response-validation framework, broad catch, retry layer, new store/parser/backfill, permission expansion, or speculative abstraction was added. The recorded simplification dispositions are honest.

### Required review questions

1. **Did the plan miss anything implied by the ticket?** The earlier malformed-successful-response shape omission was identified in review and is now fully closed by [[PR-037]]; no remaining omission found.
2. **Did implementation miss anything in the plan?** No. The final 31-file diff implements the planned behavior and evidence.
3. **Did the simplification pass run honestly?** Yes. Reuse, simplification, efficiency, and altitude findings are recorded with applied or justified dispositions; no unapplied finding remains hidden.

### CI

Exact-head repository-check run `32373963328`, final attempt 3, is fully green: changes, documentation, local-development-scripts, reference-data, infrastructure, unit, browser, SQL integration shards 1–3, and SQL integration coverage all succeeded. The first SQL attempt had unrelated LocalDB contention/timeouts; the targeted replacements passed at the unchanged head.

### Verdict

**PASS.** No unresolved review finding remains. PR #469 is eligible to merge to `dev`.
