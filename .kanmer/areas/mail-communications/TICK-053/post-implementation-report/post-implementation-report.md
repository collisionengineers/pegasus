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
