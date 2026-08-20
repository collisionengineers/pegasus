# Post-implementation report — TICK-053 / MAIL-11

## Outcome

Implemented MAIL-11 on `task/tick-053-mail-browse-search` and opened [PR #469](https://github.com/collisionengineers/pegasus/pull/469) against `dev`.

Retained Inbox search now filters in SQL before count/paging across the retained body, attachment filenames, and one receipt-owned projection of attachment text already produced by the canonical intake reader. Results disclose exact match locations and message detail discloses attachment searchability.

Deleted Items search is an explicit Core-authorized, GET-only Graph read against exact pollable approved mailboxes and each mailbox's resolved well-known `deleteditems` folder. It scans at most the 100 newest messages, passes MIME once through the existing `IIntakeSourceReader`, persists nothing, and reports unavailable/truncated states. No mailbox mutation, backfill, historical reconstruction, second parser, search store, feature framework, or deployment was added.

## Implementation

- Core: extended the retained-mail request/result contract, added typed match evidence and the bounded Deleted Items use case/port, and projects mailbox-reader content only through `IntakeSearchProjection`.
- Infrastructure: added receipt-owned `IntakeSearchDocuments`, atomic store/replace behavior, migration/model snapshot and runtime grants; extended the existing Graph client/source and approved-mailbox composition.
- Web: reused `/Inbox`, existing tabs/filterbar/paging/freshness conventions, preserved search scope through detail/back/section links, and rendered honest empty/unavailable/truncated/searchability evidence.
- Documentation: updated `docs/capabilities.md` and `docs/current-architecture.md` to the local implementation tier only. `docs/operations.md` is unchanged because nothing was deployed.
- Release checks: added the new grant-carrying migration to the existing Azure database-bootstrap permission matrix after CI's deployment-plan guard identified the required reconciliation.

## Commits

- `2d7c1421fcc4ff864cd9e114c904a3181c3fb4b9` — code, schema, UI and focused tests.
- `72f55b8a551336ffd46b8536aafdc334c1854f26` — exact local documentation.
- `93c06957` — database-bootstrap grant matrix reconciliation.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Focused `RetainedMailTests` — 25 passed.
- Focused migration/SQL/Graph/Web acceptance set — 51 MAIL-11-relevant tests passed.
- `CommittedMigrationCreatesTheSqlServerSchema` after inventory update — passed.
- `scripts/Test-MigrationGrants.ps1` — passed, 58 migrations checked.
- `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- Full Core suite — 763 passed.
- Full Architecture suite — 98 passed.

A first full Integration run found only the newly expected migration-name inventory and passed 799 other tests; the inventory was fixed and its exact rerun passed. Subsequent full runs overlapped other agents' LocalDB suites and repeatedly hit unrelated, pre-existing `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` deadlocks and one EVA SQL timeout. Even an exact selected allocation test deadlocked while other testhosts remained. The ticket-focused SQL/Graph/migration/Web set passed; CI is the clean full-suite authority. Fourteen corpus-dependent tests were intentionally skipped by the repository harness.

## Simplification and scope disposition

The dated four-lens pass is recorded in the plan. It extracted the pure projection from a temporary partial-class shape, reused the existing filterbar, removed unnecessary eager loads, kept page match grouping bounded to 25 rows, and confirmed the change stays in the existing Core/Infrastructure/Web boundaries. Actual hand-edited scope remained close to the estimate; EF's generated designer dominates the line count. No deferred capability or speculative abstraction was pulled in.

## Residual verification

Production tenant permission and real Deleted Items contents are not proven locally. After deployment, the already-approved authenticated read-only browse/search/thread journey remains the live acceptance step. No external or cloud write was performed.

## Review-blocker follow-up — 2026-08-20

Addressed [[PR-015]], [[PR-016]], [[PR-017]], [[PR-018]], [[PR-019]], [[PR-020]], [[PR-021]], and [[PR-022]] in commit `347f5ce741e19e6973a31655cd433f5c452005b0`; merged current `origin/dev` in `8b300043182ab14e8716323f6fa6f800bc2ba782`. PR remains #469 and targets `dev`.

### Outcomes

- Production Web now resolves the real Graph Deleted source; fallback profiles remain unavailable.
- Cross-mailbox Deleted search gathers at most 101 metadata candidates per selected approved mailbox, globally selects the newest 100, and reads MIME only for that global set.
- Deleted mailbox tabs come from the approved estate even when no retained Inbox row exists.
- Attachment searchability and match labels use attachment occurrence ordinals, so duplicate filenames do not share searchability.
- Retained no-match and overlong direct-query states render supported, honest responses.
- HttpClient timeout maps to unavailable while caller cancellation propagates.
- Operator activation of local MAIL-11 implementation is recorded in design/capability authority; no deployment, permission, or mailbox-write authority is claimed.
- No historical backfill, second parser, second projection/store, or external write was added.

### Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- Release solution/Web/Infrastructure/Integration project builds — passed, zero warnings/errors on the captured clean build.
- Focused Core retained-mail tests — 26/26 passed.
- Focused production Graph/composition tests — 31/31 passed.
- Exact web no-match/invalid-query test — 1/1 passed.
- Exact retained search projection/persistence test — 1/1 passed after correcting the fixture to carry attachment ordinal; migration round-trip test also passed.
- `dotnet ef migrations has-pending-model-changes ...` — no pending model changes, including after merging current `origin/dev`.
- `scripts/Test-MigrationGrants.ps1` — 59 migrations checked, passed.
- `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- One combined focused LocalDB run stalled during known shared LocalDB contention and was stopped by exact verified process IDs; exact isolated owning reruns above passed.

### Exact final PR inventory (28 files)

The original reviewed 26-file inventory is complete below; the final total is 28 because the authorized blockers added the production-composition proof and the operator-activation design owner.

1. `docs/capabilities.md` — records MAIL-11/UI-10 local implementation, activation, bounded cross-mailbox behavior, and separate deployment evidence.
2. `docs/current-architecture.md` — records the local retained/search read shape without claiming deployment.
3. `docs/design/README.md` — records the operator-approved MAIL-11 local re-entry and remaining release evidence boundary.
4. `scripts/Invoke-AzureDatabaseBootstrap.ps1` — grants the existing runtime roles access to the new projection table.
5. `src/Pegasus.Core/Intake/DeletedMailSearch.cs` — owns authorization, 100-message policy, paging, mailbox listing port, and unavailable contract.
6. `src/Pegasus.Core/Intake/IntakeContracts.cs` — carries canonical reader attachment identity and the single search projection contract.
7. `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` — projects existing canonical reader output once, by exact attachment occurrence.
8. `src/Pegasus.Core/Intake/ProcessIntake.cs` — supplies that projection to the existing receipt draft path.
9. `src/Pegasus.Core/Intake/RetainedMail.cs` — extends the existing retained query/search match contracts.
10. `src/Pegasus.Infrastructure/DependencyInjection.cs` — composes retained queries/use cases and preserves the explicit production Graph source over the fallback.
11. `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — performs bounded fair approved Deleted metadata/MIME reads, timeout mapping, and canonical-reader search.
12. `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` — annotates its existing attachment descriptors with occurrence/source identity.
13. `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — persists the receipt-owned search projection and attachment ordinal.
14. `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` — filters before paging and maps exact search matches/searchability.
15. `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.Designer.cs` — generated model metadata for the projection table and ordinal.
16. `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.cs` — creates the projection table/grants, with no backfill.
17. `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` — current EF model snapshot.
18. `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — maps the receipt-owned projection entity and exact attachment ordinal.
19. `src/Pegasus.Web/Pages/Mail/Index.cshtml` — renders GET search, bounded Deleted results, honest validation/no-match/unavailable states, and preserved scope.
20. `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` — selects retained versus approved Deleted mailbox sources and coordinates queries without mutation.
21. `src/Pegasus.Web/Pages/Mail/Message.cshtml` — preserves search scope through detail/back navigation and shows attachment searchability.
22. `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — carries the search term through the existing detail model.
23. `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` — proves Core bounds/authorization and duplicate-filename occurrence projection.
24. `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` — asserts the migration inventory and projection table after current-dev reconciliation.
25. `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — proves GET search UI, scope, no-match, blank, and overlong behavior.
26. `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` — proves production Graph composition wins and fallback composition stays unavailable.
27. `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` — proves exact-folder reads, fair cross-mailbox selection, approved zero-row mailbox choices, timeouts, and cancellation.
28. `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` — proves persisted body/name/content matches and exact searchable attachment mapping.

## Remaining review-blocker follow-up — 2026-08-20

Addressed [[PR-017]], [[PR-018]], [[PR-024]], and [[PR-025]] in `c0fa9a9905f2808ec1e2eb03e42dbe29cfde7ae4` on PR #469.

Authenticated `/Inbox?folder=deleted` evidence now proves approved mailbox selection with zero retained rows, exact selected scope/search/fixed 100-message bound, visible match location, truncation, 25/1 paging, and unavailable state. Nameless MIME attachments remain in the same canonical occurrence order as later named attachments; retained detail renders each attachment's `IsSearchable` disclosure. Retained SQL admission now prevents root canonical wrapper text that is absent from the displayed retained body from producing an unlabeled result. No historical backfill, second reader/projection/store, external write, Graph permission, or deployment change occurred.

Verification: `dotnet build Pegasus.slnx --configuration Release --no-restore` passed with 0 warnings/errors. The focused remaining-blocker set (production Graph source, authenticated Deleted Web caller, retained detail disclosure, nameless identity, and retained persistence search) passed 25/25. `git diff --check` passed.

### Corrected exact final PR inventory (30 files)

The prior 28-file inventory omitted two already-diffed production files. Its numbered rationales remain accurate; add:

29. `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs` — maps the existing MIME display into retained metadata, now preserving nameless attachment occurrences so canonical ordinals cannot shift.
30. `src/Pegasus.Web/Presentation/OperatorLabels.cs` — remains the single operator-facing owner for the attachment-searchability wording used by retained and Deleted views.

`git diff --name-only origin/dev...HEAD` reports exactly 30 files. This corrects the inventory only; it does not expand scope.

## Final review-blocker follow-up — 2026-08-20

Merged current `origin/dev` in `6e52935e9065f0769c2629015202909186f5625c` and addressed [[PR-018]], [[PR-024]], [[PR-029]], [[PR-030]], and [[PR-031]] in `7932d683782669e112f3d996c6914323e8ba72d4` on PR #469.

- Attached `text/plain` parts now preserve canonical/display ordinal identity.
- The one receipt root projection is route-aware and normalized; retained SQL body admission, match labeling, and visible detail read the same text, with no reconstruction.
- A nonmatching retained thread member is explicitly outside the preserved active search.
- Deleted MIME GETs remain bound to the resolved folder; a concurrent move fails to unavailable.
- Azure credential acquisition failure maps to unavailable through the authenticated caller while caller cancellation remains distinct.

### Verification

- Release solution build: passed, 0 warnings/errors.
- Core retained-mail class: 27/27 passed.
- Focused Graph/Web/SQL blocker slice: 27/27 passed.
- Complete owning Web + persistence classes: 38/38 passed.
- Exact normalized-body SQL rerun: 1/1 passed.
- `git diff --check`: passed.

### Exact final PR inventory

`git diff --name-only origin/dev...HEAD` remains exactly 30 files; no new file entered the PR. The prior numbered 30-file inventory remains complete. Final-blocker rationale updates for the 11 affected existing entries are:

- `src/Pegasus.Core/Intake/IntakeSearchProjection.cs` — creates the normalized visible/search root from existing reader fragments and route evidence.
- `src/Pegasus.Core/Intake/ProcessIntake.cs` — passes the already-computed route decision into that single projection.
- `src/Pegasus.Core/Intake/RetainedMail.cs` — carries one normalized active term through the existing detail boundary.
- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — folder-scopes Deleted MIME and maps Azure authentication failure to unavailable.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` — preserves attached text parts in canonical ordinal identity.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` — uses the same root for SQL admission, labels, detail body, and detail search membership.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — marks nonmatching thread members outside the originating search.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` — proves forwarded wrapper/cid normalization.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — proves authenticated thread mismatch and credential-unavailable rendering.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` — proves exact folder MIME path and concurrent-move unavailability.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` — proves attached-text ordinal stability and normalized root SQL/detail equality.

No external/cloud/mailbox write, deployment, Graph permission change, historical backfill, merge, or self-review occurred. Replacement CI remains the independent clean-suite authority.

## Independent-final-blocker follow-up — 2026-08-20

Addressed [[PR-033]], [[PR-034]], [[PR-035]], and [[PR-036]] in `fc6840361c1c19ece9a75d7ea68c713c75d01b75` on PR #469.

- Successful-but-invalid Graph responses (malformed JSON, missing identity/time, foreign folder, escaped next link) now map to Deleted unavailable rather than 500; caller cancellation remains distinct.
- Explicitly attached Content-ID images remain in canonical/display attachment occurrence order, completing [[PR-018]].
- Invalid classification-correction POSTs keep supported search behavior: whitespace normalizes to the existing unfiltered page, overlong input returns 404, and neither writes history or returns 500.
- Worker projection permission is exactly `SELECT, INSERT, DELETE`; unsupported UPDATE is absent from migration/bootstrap and proven against a freshly migrated database.
- No mailbox/cloud write, deployment, Graph permission change, backfill, second parser/store, retry/validation framework, merge, or self-review occurred.

### Verification

- Release solution build: passed, 0 warnings/errors.
- `ProductionGraphSourceTests`: 27/27 passed.
- Exact new attachment/Web/SQL proofs: 3/3 passed.
- Complete `MailWorkspaceWebTests + RetainedMailPersistenceTests`: 39/39 passed.
- `scripts/Test-MigrationGrants.ps1`: passed, 59 migrations checked.
- `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`: passed.
- `git diff --check`: passed.

### Exact final PR inventory (31 files)

The prior numbered 30-file inventory remains complete. One established permission-evidence file entered the diff:

31. `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` — proves Web SELECT only and Worker SELECT/INSERT/DELETE on `IntakeSearchDocuments`, including absence of UPDATE, on a fresh migrated database.

Updated rationales for the nine already-inventoried files changed by this pass:

- `docs/current-architecture.md` — states the exact caller-backed Worker projection permission set.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` — mirrors that exact permission set in the deployment census.
- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — maps established malformed/scope-invalid response exceptions to unavailable.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` — gives explicit attachment disposition precedence over Content-ID.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.cs` — removes unsupported Worker UPDATE from the unmerged migration.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — handles invalid Core search context during correction reload.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — proves authenticated whitespace/overlong correction reload behavior and no writes.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` — proves all invalid Graph response categories return unavailable.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` — proves explicit Content-ID attachment/display ordinals through a later searchable PDF.

`git diff --name-only origin/dev...fc684036` reports exactly 31 files.

### Current-dev reconciliation

Merged current `origin/dev` (including PR #474 / [[TICK-047]]) in `eaf2f9f4eac577242ed301dd917f0682d4a77729`. The resolved Core path preserves MAIL-11 search context and then derives TICK-047's folder recommendation; both test fakes remain. Post-merge Release build passed with 0 warnings/errors, Core retained-mail tests passed 34/34, and the three new attachment/Web/SQL proofs passed 3/3. The PR is conflict-free and still reports exactly 31 files against current `origin/dev`; CI is queued at the merge head.

## PR-037 malformed-page follow-up — 2026-08-20

Addressed [[PR-037]] in `6aaf2418c30defc1fb21111a10b954e70f74eea3` on PR #469, completing [[PR-033]].

The existing Graph client now validates that the successful Deleted folder root is an object, that a Deleted message page is an object with array `value`, and that any present next link is a valid absolute URI. These exact parse failures become the existing `InvalidDataException` and therefore the existing unavailable state; the outer catch was not broadened.

Verification: Release build passed with 0 warnings/errors; `ProductionGraphSourceTests` passed 33/33; authenticated malformed folder-root/page/next-link Web evidence passed 3/3; `git diff --check` passed. The three affected files were already inventoried, so the exact PR inventory remains 31 files:

- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — now validates every Deleted successful-response envelope/link before parsing.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` — covers non-object folder/page roots, missing/non-array value, and invalid/relative next links.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — proves representative authenticated failures render unavailable.

No external write, deployment, Graph permission change, backfill, new framework, merge, or self-review occurred.
