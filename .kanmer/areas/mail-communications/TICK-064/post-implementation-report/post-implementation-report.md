# Post-implementation report — TICK-064

*This is the author's pre-merge claim, not post-merge proof.*

## Summary

Implemented MAIL-23 as one canonical Core projection from the settled mail-classification vocabulary to 13 logical Outlook folder types or an explicit no-recommendation result. Exact folder identities are now administrator-approved bindings owned by the existing approved-mailbox aggregate, resolved through GET-only Graph discovery confined to that mailbox, persisted in one normalized child collection, and shown honestly as configured/unconfigured in the existing administration page. MAIL-02 queues, retained-message persistence, MAIL-05 recommendation consumption, and MAIL-07 move execution remain unchanged.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/Classification/MailLogicalFolderPolicy.cs` | added | Single Core owner for the 13-value logical-folder vocabulary and exhaustive classification projection. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | modified | Adds validated mailbox-owned typed bindings with omitted-preserve / explicit-replace semantics and replay material. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | modified | Adds GET-only recursive exact-folder discovery, paging confinement, and duplicate-name fail-closed handling. |
| `src/Pegasus.Infrastructure/Email/LocalApprovedMailboxIdentityResolver.cs` | modified | Supplies deterministic local/offline bindings through the existing resolver port. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` | modified | Adds the approved-mailbox child binding entity. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs` | modified | Configures the normalized composite key, bounds, relationship, and cascade ownership. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` | modified | Loads, preserves/replaces, snapshots, and replays bindings in the existing transaction/history path. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100056_ApprovedMailboxLogicalFolderBindings.cs` | added | Creates the binding table and grants the Web runtime its required table permissions. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100056_ApprovedMailboxLogicalFolderBindings.Designer.cs` | added | Generated migration model metadata. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | modified | Records the additive binding model. |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs` | modified | Adds the administrator-only server-resolved refresh action and persists bindings on add. |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml` | modified | Displays canonical labels and configured/unconfigured state without exposing folder identities. |
| `tests/Pegasus.Core.Tests/Intake/Classification/MailLogicalFolderPolicyTests.cs` | added | Exhaustively proves every registered classification, no-recommendation outcomes, reply invariance, and queue separation. |
| `tests/Pegasus.Core.Tests/Identity/AdministrationPolicyTests.cs` | modified | Proves binding normalization and validation. |
| `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs` | modified | Proves persistence, preserve/replace, replay, and conflict behavior. |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs` | modified | Proves the authorized server-side caller and honest no-ID display. |
| `tests/Pegasus.IntegrationTests/GraphApprovedMailboxResolverTests.cs` | modified | Proves GET-only discovery, nesting, duplicate omission, and hostile paging confinement. |
| `tests/Pegasus.IntegrationTests/LocalApprovedMailboxIdentityResolverTests.cs` | modified | Proves deterministic offline bindings. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | modified | Extends the single committed-migration/schema proof for the new table. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | modified | Reconciles the already-settled Unidentified vocabulary and MAIL-23/05/07 ownership split. |

## Governing docs

The change implements linked FRD-08's logical-folder catalogue, explicit no-automatic-folder behavior for ambiguous/unclassified results, and administrator-approved exact mailbox bindings. The narrow FRD edits are authorized by the task's governing-doc step and reconcile existing accepted facts: INTK-007's `Unidentified` term and the settled separation of MAIL-23 binding, MAIL-05 recommendation, and MAIL-07 confirmed move. No ADR is needed because the existing Core, Infrastructure, EF, and Web boundaries carry the change. No current-state document changed because nothing was deployed and no live mailbox was queried or written.

## Risks / follow-ups

- A production mailbox can remain partially or wholly unconfigured; duplicate or missing exact labels intentionally produce no binding rather than a guess.
- MAIL-05 remains the future message-level recommendation consumer, and MAIL-07 remains the only future confirmed Graph move owner.
- The canonical non-corpus run occurred while another ticket contended for shared LocalDB. Core (828) and Architecture (98) passed; Integration passed 798/799 with one unrelated SQL execution timeout in `IntakeWebNegativeTests.UnknownExtensionReachesReaderAndPersistsUnsupportedReceipt`. The ticket-owned focused tests, committed-schema test, and the earlier transient Qdos exact rerun all pass. Per coordinated instruction no additional full suite was started.

## Verification hand-off

On merged `dev`/release candidate, run:

- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` with exclusive LocalDB access
- focused Core `MailLogicalFolderPolicyTests` and `AdministrationPolicyTests`
- focused Integration `GraphApprovedMailboxResolverTests`, `LocalApprovedMailboxIdentityResolverTests`, `AdministrationPolicyPersistenceTests`, `ApprovedMailboxAdministrationWebTests`, and `CommittedMigrationCreatesTheSqlServerSchema`

For UI evidence, use the offline/local resolver and capture the Mailboxes administration page showing canonical configured/unconfigured labels and the administrator refresh action. Do not use a live Outlook mailbox without separate exact-target approval. Expected result: no folder identity is rendered, refresh accepts no client-supplied folder id, and Graph fakes observe GET only.

## Delivery-gate addendum — 2026-08-20

The first PR repository check identified the existing exhaustive Azure bootstrap permission matrix as an additional changed file. `scripts/Invoke-AzureDatabaseBootstrap.ps1` now accounts for migration `20260820100056_ApprovedMailboxLogicalFolderBindings` with the exact Web-only `SELECT`, `INSERT`, and `DELETE` grants; the Worker has no caller and no grant. Commit `b6754dd8` contains the correction. Local `Test-AzureDeploymentPlan.ps1 -Mode Local` passes, and `Test-MigrationGrants.ps1` passes across 58 migration files. The initial shard-coverage check failed only because the failed changes gate skipped its producer shards; the new push reruns the workflow.
