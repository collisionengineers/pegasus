# Post-implementation report — MAIL-07

## Outcome

Implemented the narrow confirmed retained-mail folder move and closed independent-review blockers PR-038 through PR-042. Authenticated staff confirm the server-derived recommendation with a reason; Core revalidates classification/policy/mailbox freshness, Infrastructure reserves one active operation per message, probes the exact current source, moves through the existing provider port, and preserves immutable arrival/classification evidence. Uncertain operations expose an authenticated same-key status check which probes only. A later reclassification can produce a second confirmation to a different exact binding. Successful moves leave ordinary Inbox browse but remain findable through the existing retained search.

Default composition still supplies no live writer. No live Graph, mailbox, cloud, permission, deployment or external write was performed.

## Review-blocker corrections

- **PR-038:** unique filtered `RetainedMailboxMessageId` index for pending/uncertain rows plus post-reservation exact source probe; overlapping different keys cannot both move.
- **PR-039:** durable result exposes only the original operation key/reason/freshness; Razor reuses them for same-key uncertain recovery; no blind repeat move.
- **PR-040:** current location is latest successful destination or immutable arrival folder; exact approved destination comparison permits a separately confirmed reclassification move.
- **PR-041:** ordinary Inbox browsing excludes successful moves, while non-empty canonical retained search includes them exactly once with current logical folder, mailbox filtering and paging preserved.
- **PR-042:** executable negative/preservation evidence replaces prior overclaims.

## Simplicity

The dated plan records reuse, simplification, efficiency and altitude. The final design keeps the existing dedicated move store, MAIL-05 recommendation, MAIL-11 search, typed bindings, Graph mover/probe and shared dialog. It adds no generic command framework, second policy/category list, second search store, destination tabs, project, runtime or deployment unit. No findings remain unapplied.

## Final changed-file inventory

Original implementation plus blocker corrections changed:

- Docs/bootstrap: `docs/capabilities.md`, `docs/current-architecture.md`, `scripts/Invoke-AzureDatabaseBootstrap.ps1`.
- Core: `src/Pegasus.Core/Intake/RetainedMail.cs`, `RetainedMailFolderMove.cs`.
- Infrastructure/provider: `src/Pegasus.Infrastructure/DependencyInjection.cs`, `Email/GraphApprovedSources.cs`, `Persistence/EfRetainedMailFolderMoveStore.cs`, `EfRetainedMailboxMessageStore.cs`, `MailboxEntities.cs`, `MailboxModelConfiguration.cs`, `PegasusDbContext.cs`.
- Existing unmerged migration stream: `Persistence/Migrations/20260820144004_RetainedMailFolderMoves.cs`, its Designer, and `PegasusDbContextModelSnapshot.cs`.
- Web: `src/Pegasus.Web/Pages/Mail/Index.cshtml`, `Message.cshtml`, `Message.cshtml.cs`.
- Tests: `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs`; Integration `AzureSqlRuntimeRoleMigrationTests.cs`, `IntakePersistenceIntegrationTests.cs`, `MailWorkspaceWebTests.cs`, `ProductionGraphSourceTests.cs`, `RetainedMailPersistenceTests.cs`.

## Verification

All provider evidence used fake HTTP/provider implementations and local SQL.

- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Focused retained-mail Core tests — 40/40 passed.
- Exact persistence blocker tests — 4/4 passed.
- Exact authenticated Web move/recovery tests — 4/4 passed; final search-enhanced happy test rerun 1/1.
- Full `Pegasus.Core.Tests` — 848/848 passed.
- `CommittedMigrationCreatesTheSqlServerSchema` — 1/1 passed.
- `RetainedMailFolderMovesUseExactWebOnlyAppendPermissions` — 1/1 passed.
- Broader retained-mail/Web/fake-Graph filter — 87 behavior tests passed; its only failure was the intentionally changed empty-search copy expectation. That assertion was corrected, rebuilt, and `SearchDistinguishesNoMatchAndInvalidInputFromNoReceivedMail` passed 1/1 on the final binary.
- `Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- `Test-MigrationGrants.ps1` — passed for 60 migrations.
- `git diff --check` and staged diff check — passed.

## Commits and pull request

- `8b1e6d74` — initial feature/persistence/migration/tests.
- `f60248af` — qualify local evidence.
- `5e8217a1` — reconcile runtime permissions.
- `fc3b651e` — close PR-038 through PR-042.

PR #477 targets `dev`: https://github.com/collisionengineers/pegasus/pull/477

## Handoff

Leave TICK-049 and all five blocker tickets in Review for an independent `kanmer-review`. Do not infer deployment/live mailbox evidence from green local/CI tests.

## PR-043 correction — same-key in-flight replay

Commit `83293162` closes the final in-flight replay race. Pending replay now returns the focused still-processing refusal with no parent probe or state write. A provider exception first persists Uncertain, retaining the active filtered slot, before the existing probe runs. Exact overlapping evidence proves the Pending row remains active, a new key is refused, the original call performs the only move, and the same key replays the completed success.

Additional final evidence: exact concurrency/Uncertain set 5/5, provider-failure/freshness/reclassification set 3/3, full retained-mail persistence class 24/24, Release solution build 0 warnings/errors, and diff checks passed. Only `EfRetainedMailFolderMoveStore.cs` and `RetainedMailPersistenceTests.cs` changed for PR-043. No external write or new framework was introduced.

## PR-044 correction — cancellation-safe uncertain handoff

Request cancellation after the Pending reservation now performs a bounded, fresh-context, conditional Pending → Uncertain handoff before rethrowing the original cancellation. Only Uncertain can enter the existing same-key probe recovery, so the active slot remains held and a different key cannot start while the outcome is unresolved. A conditional update leaves an already committed Success unchanged.

Exact evidence: focused cancellation/concurrency/recovery set 6/6; full retained-mail persistence class 26/26; Release solution build passed with 0 warnings/errors; diff check passed. Both cancellation tests recover with the original key through probes, refuse a different key until resolution and keep the provider move count at one. Only `EfRetainedMailFolderMoveStore.cs` and `RetainedMailPersistenceTests.cs` changed. No external write, migration, worker, lease or generic framework was introduced.

Commit `1cc0927d22bc4976ecb4e8b5491658a9db3eedd3` delivers PR-044 on PR #477.
