# Plan — TICK-054 recoverable exact-message Outlook actions

## Objective

From one opened retained message, authorised staff can deliberately set
read/unread, add/remove one configured category, flag/unflag, move to Deleted
Items with a reason, and restore to its server-recorded prior approved folder.
Retained source evidence never changes. No permanent-delete operation exists.

## Starting state

Source: accepted dev `19e6f523bf6760cab39104b4dca3674b0ac8a512`.
Evidence: `research/research.md`@`22e4a219a7da018e`,
`files/files.md`@`b8ec0443c3eb4d64`.
Resolved questions version: e58a7bab450fa458.

This replaces plan `f3f565c096f44a0c` and its obsolete permanent-delete journey.
The settled August 20 boundary and current ticket body supersede that journey;
no unresolved permanent-delete decision remains. TICK-049's mover and
MAIL-004's catalogue are integrated. Existing Graph move/probe transport is
unavailable by default; MAIL-028 owns its activation. MAIL-031 is downstream
Administration, not a prerequisite. No historical PLAT-075 claim is acquired.

Astra DEFERRED-WORK.md explicitly left this scope in TICK-054/MAIL-028 and
excluded MAIL-026/027 flag/delete clauses. The latest user task reactivates this
Preparing residual. Existing send code is not evidence that these actions exist.

## Governing docs

**Meets** the linked FRD-08: exact durable mailbox/message identity, approved
category IDs, deliberate exact-message action, retained evidence/history and
recoverable-only Delete. Add one concise MAIL-13 subsection to clarify current
state/read/unread/restore/recovery; qualify the old read-only workspace sentence
as browsing/navigation, which never marks read. Preserve classification and
association as different commands. No protected business meaning changes.

Meets EPIC-006 and EPIC-011 D4/D22 and the design authority: one Core owner,
no bulk/preview actions, no permanent deletion, existing accessible reason
confirmation and absent controls when unavailable. The existing Core/EF/Graph
boundary fits; no new ADR, runtime, package, generic action framework or store
service is required. A one-row-per-message current-state projection exists
solely to meet the explicit immutable-arrival versus current-Outlook distinction.

## Required changes

- Extend the existing retained-mail boundary with a **closed** action set:
  MarkRead, MarkUnread, AddCategory, RemoveCategory, Flag, Unflag, DeleteToDeletedItems,
  Restore. Keep ordinary MoveRetainedMailFolder's existing classification-based
  rule. Share its per-message operation journal and exclusion; do not invoke
  that rule with fabricated recommendation fields for Delete/Restore.
- Inputs: internal retained message ID, expected mailbox and current-state
  versions, expected provider version, operation key, selected internal category
  ID when applicable, and required reason for Delete/Restore. Staff identity
  comes from authentication. Require Staff + PerformCasework in Core; recheck
  current authority/binding immediately before a fresh effect. No browser-supplied
  mailbox, Graph ID, arbitrary category text or destination is accepted.
- Add a small `RetainedMailOutlookStateEntity` projection in the **existing**
  EF owner: retained-message PK/FK, observed read/category/flag/current parent,
  current scope, changeKey/ETag, UTC observed time and optimistic version.
  Preserve arrival metadata, source bytes/hash, classification, case links and
  Sent evidence. No new store interface/service or second operation journal.
  Extend the existing operation row with explicit action, expected-state and
  bounded before/after evidence; classification-only fields apply only to the
  designated move. Reuse the existing active-operation uniqueness constraint.
- An explicit message-state refresh reads that exact provider item, records its
  observation, and never changes Outlook; normal navigation/preview does not
  mark read or create business history. Mutation preflight reads the item again
  and refuses stale/mismatched identity, mailbox or provider/current-state
  version. The current-state projection, not arrival IsRead or 'any move ever',
  drives the same query owner's effective unread/folder list and counts.
  Keep the historical arrival fields separately identifiable and show the last
  observed time, stale at the existing fixed 15 minutes, or unavailable.
- Resolve category ID to the existing active exact name/version and preserve
  every unrelated current category. No master-category/colour/name creation.
  Flag writes only flagged/notFlagged; no completion/due-date workflow.
- Delete resolves the exact mailbox's Deleted Items ID with the existing
  resolver and records the actual pre-delete approved folder. Restore requires
  the matching successful Pegasus delete record, current Deleted Items parent,
  unchanged approved mailbox and a still-approved original folder. No target
  picker, fallback Inbox, cross-mailbox move or restoring an externally deleted
  item without a recorded target. A valid restore makes that same retained row
  reachable in its effective original scope, never creates a new intake/case.
- Keep external work outside a SQL transaction. Within this same owner, reuse
  the SQL session application-lock convention demonstrated by
  `SqlStaffMailExecutionLock`, keyed by retained-message ID, to exclude an
  active request from competing recovery; do not introduce another lock service.
  Persist the existing operation reservation before any effect. Same-key input
  mismatch fails. Completed replay returns its recorded result without writing
  Graph. A reconstructed pending/uncertain operation performs **only a probe**,
  never repeats PATCH/move. Unresolved/missing/contradictory observations remain
  uncertain and occupied, visible through Check status. Confirmed target state
  is recorded as observed reconciliation, not invented causal proof. A definite
  pre-effect refusal may fail; no new key bypasses an occupied operation.
- Reuse GraphMailClient host/token/immutable-ID conventions. Read bounded exact
  metadata; PATCH only the chosen property's representation. Send the returned
  ETag unchanged as If-Match, reject absent conditional state for a fresh PATCH,
  and surface 412 without unconditional retry. Use the existing scoped move
  endpoint for Delete/Restore, then verify the resulting parent. Cancellation,
  timeout, malformed/oversized response or lost commit after a possible effect
  retains uncertainty. No DELETE or permanentDelete URL/method exists.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs` | Extend the existing exact-message use-case/store/transport boundary with closed read/category/flag/delete/restore actions and current-state results. Ordinary designated moves retain their classification rule; new actions cannot forge it. |
| Modify | `src/Pegasus.Core/Intake/RetainedMail.cs` | Expose separately labelled arrival and observed Outlook state, freshness, current effective scope and allowed exact-message actions. |
| Modify | `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs` | Add the staff-authorized active-choice read needed by the message page, using the existing store/resolver and single catalogue. No new administration or master-category sync. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` | Existing journal owner carries all these concrete actions, shared per-message exclusion, replay/recovery, current observation and recorded restore target. No second move store or workflow. |
| Modify | `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Extend existing operation row with closed action kind/expected-state/before-after evidence; add one per-retained-message mutable Outlook-state projection without changing arrival fields. |
| Modify | `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Journal constraints and unique pending/uncertain exclusion; current-state row FK/version and no cascading evidence deletion. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Single effective unread/current-folder projection for detail, list/counts and retained search; restored Inbox items reappear without re-intake. |
| Modify | `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Reuse GraphMailClient and GraphRetainedMailFolderMover; exact metadata read/conditional property PATCH and existing move/Deleted Items resolver. No DELETE/permanentDelete endpoint. |
| Modify | `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register added Core commands in the existing composition; keep unavailable production transport default. MAIL-028 owns activation. |
| Modify | `src/Pegasus.Infrastructure/Persistence/Migrations/*RetainedMailMessageState*.cs` | One normal migration and generated designer only, current-state projection plus narrow journal changes; exact Web grants/DELETE denial in same diff. No historic migration edits. |
| Modify | `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Generated current EF model only. |
| Modify | `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Same-migration grant census and least-privilege matrix, no broad grants or execution. |
| Modify | `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Existing exact-message toolbar/detail and reason dialog pattern; actions only here. |
| Modify | `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Thin authenticated/antiforgery action and read-only reconciliation handlers with server-resolved identity/state; preserve originating list context. |
| Modify | `src/Pegasus.Web/Presentation/OperatorLabels.cs` | One label vocabulary for named actions/states. |
| Modify | `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs` | Closed action/actor/destination/category and expected-state policy cases. |
| Modify | `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs` | Staff active choices and disabled/forged category refusal. |
| Modify | `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Existing SQL harness: shared exclusion/replay/restart, evidence preservation, restore target and effective list/counts. |
| Modify | `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Existing fake HTTP: exact identities/headers/property confinement, 412 and uncertain responses, read-only recovery. |
| Modify | `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Existing authenticated real-page harness: controls, antiforgery/version/refusal/recovery and minimal actual route captures. |
| Modify | `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Prove actual Web owner can execute its state/journal writes; retained evidence DELETE remains refused and Worker does not acquire a mail mutation grant. |
| Modify | `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` | Update the known exact pending-migration inventory when the new migration is generated; do not repeat DOCS-020's omitted-list failure. |
| Modify | `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Clarify settled MAIL-13 action/current-state/restore/recovery semantics; retain read-only browsing and permanent-deletion prohibition. |
| Modify | `docs/capabilities.md` | State implementation vs activation accurately, not Done from registration. |
| Modify | `docs/current-architecture.md` | Describe the actual shared owner and explicit still-closed activation state after implementation. |
| Modify | `docs/design/test-ui/pages/inbox-message--*.html` | Generated captures owned by the changed route, only measured states. |
| Modify | `docs/design/test-ui/index.html` | Generated capture metadata if changed by the existing update script. |

## Do not modify

- `docs/operator-notes.md`

## Constraints

Every path outside Expected files is undeclared. Preserve the classification
folder taxonomy, staff sending/compose, Worker, shared CSS/JS/reason dialog,
infra, corpus/reference data, old migrations, foreign claims/worktrees and
MAIL-031 Administration. MAIL-028 retains activation. No new SMTP, Graph SDK,
background retry, mailbox sync or generic command dispatcher.

Root is the sole heavy verifier. The author runs no tests/builds without root's
scheduled instruction, and performs no cloud/mail writes or deployment. Existing
production transport remains unavailable until MAIL-028's separate activation;
that is not delivery evidence. Local SQL/fake HTTP may exercise the real adapter
through injected fixtures without touching Outlook. Do not add a new feature
flag or permission system to conceal incomplete implementation.

New schema and the exact Web SELECT/INSERT/UPDATE grant plus DELETE denial,
bootstrap census and runtime-role test ride the same diff. No Worker mutation
grant; no permission to modify/delete immutable retained-source columns. Add the
one new migration to the existing exact migration inventory. No historic schema
rewrite or preservation framework.

## Ordered steps

### Step 1 — Extend the existing Core action boundary

- Preconditions: root approves this current plan and assigns a fresh execution packet; integrated TICK-049/MAIL-004 owners are present.
- Tests: RetainedMailFolderMoveTests; ApprovedOutlookCategoryTests.
- Commands: no author build/test; submit the named Core filter to root.
- Preserved behaviour: ordinary MAIL-07 designated movement and catalogue administration remain unchanged.
- Negative cases: non-staff, missing authority, forged category/destination, stale expected state and absent restore origin refuse.
- Expected output: focused Core assertions pass under root verification; no new package or permanent-delete enum.
- Deviation stop: a new policy owner, unapproved behaviour or undeclared file is needed.

- Files: `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs`, `src/Pegasus.Core/Intake/RetainedMail.cs`, `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs`, `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs`, `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs`.
- Change: the closed action requests/results, active category choices and current-state view; Core owns authorization, target eligibility and action semantics. Preserve the ordinary designated-folder move contract.
- Done when: direct Core actor/forged target/category/stale-state refusals and successful action policy cases are represented by focused tests; no permanent-delete branch exists.

### Step 2 — Implement durable state, existing transport and recovery

- Preconditions: Step 1 contracts are fixed; root coordinates the current migration number and shared-file ownership before edits.
- Tests: MessageState cases in RetainedMailPersistenceTests/ProductionGraphSourceTests, affected AzureSqlRuntimeRoleMigrationTests and CaseWorkflowMigrationTests.
- Commands: no author build/test/cloud; root runs the agreed focused SQL/fake-HTTP/role filter and migration grant check.
- Preserved behaviour: immutable arrival/custody/Case/Sent evidence, one existing journal and unavailable live composition.
- Negative cases: stale provider/binding, two active keys, key reuse, absent restore target, unknown/cancelled effect and denied SQL role.
- Expected output: one effect maximum per reserved action, uncertain work remains recoverable and occupied, exact grants pass.
- Deviation stop: conditional provider semantics cannot be met without an unsafe fallback or a second store/service.

- Files: `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`, `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`, `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs`, `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`, `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs`, `src/Pegasus.Infrastructure/DependencyInjection.cs`, `src/Pegasus.Infrastructure/Persistence/Migrations/*RetainedMailMessageState*.cs`, `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`, `scripts/Invoke-AzureDatabaseBootstrap.ps1`, `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`, `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs`, `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`, `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs`.
- Change: one journal/exclusion owner and separate mutable observation row; exact PATCH/move/probe, restore target, effective list projection and same-key restart recovery. Schema/grants/census together.
- Done when: no provider write precedes durable reservation/current checks; reconstructed unknown work only probes; unrelated categories and retained source survive; restored Inbox row/count return; fake/local compositions do not enable a live writer.

### Step 3 — Wire the exact message page and governing behaviour

- Preconditions: Step 2 use cases and stored results exist; the page consumes those decisions without new UI policy.
- Tests: MessageState cases and the default message-detail route in MailWorkspaceWebTests.
- Commands: no author runtime run; send the smallest changed route capture names to root.
- Negative cases: missing antiforgery/authentication, forged state/category/destination, stale confirmation, unavailable composition and cancelled dialog.
- Expected output: only deliberate exact-message POSTs can mutate through Core; keyboard/focus/context remain intact.
- Deviation stop: shared CSS/dialog redesign, compose/send or Administration policy is required.

- Files: `src/Pegasus.Web/Pages/Mail/Message.cshtml`, `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`, `src/Pegasus.Web/Presentation/OperatorLabels.cs`, `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`, `docs/frd/frd-08-email-mailbox-and-background-processing.md`, `docs/capabilities.md`, `docs/current-architecture.md`.
- Change: named authenticated POST handlers under `/Inbox/{id}` call Core only. Reuse the existing toolbar, status/error feedback and reason dialog markup/pattern. Read/unread, category and flag controls use explicit actions, not automatic navigation effects; Delete/Restore require Confirm/Cancel and reason. Check status uses the original operation key and never mutates Graph. Refresh is explicit, not background polling.
- Preserved behaviour: list filters/page/selected message, retained detail outside the original scope, keyboard access and focus return. No rows/preview/bulk mutations, explanatory panels or unrelated page redesign; reflow with existing styles at 1580/1100/760px.
- Done when: actual page tests prove anti-forgery, server identity/version binding, unavailable controls absent, pending/uncertain/replay/conflict feedback, and no permanent-delete UI/route.

### Step 4 — Root-owned focused verification and evidence handoff

- Preconditions: source is frozen for root and actual capture paths/filter names have been agreed.
- Tests: the focused Core/SQL/fake-HTTP/Web/role cohort listed below; existing Test UI update/verify and catalogue.
- Commands: root runs the Commands section once against the recorded author head; no author parallel build/test.
- Preserved behaviour: all failure evidence remains and local passes never become live-activation claims.
- Negative cases: missing capture, stale snapshot, failed role/caller check or uncertain provider fact cannot be marked PASS.
- Expected output: recorded exit-zero focused results, exact TRX/capture artifacts and reviewed scope report.
- Deviation stop: any failing result is returned for a bounded correction; no repeated whole-suite loop.

- Files: `docs/design/test-ui/pages/inbox-message--*.html`, `docs/design/test-ui/index.html`.
- Change: root runs the scheduled focused cohort below, captures the actual message route, updates/verifies only that scope and checks catalogue/grants. Author records every attempt, source/head and exact outcomes in the ticket report; no failure is erased by later recovery.
- Done when: root provides passing bounded evidence, author reconciles the checklist/report and hands the unchanged reviewed scope to independent review. No self-review, merge, activation or live journey is claimed here.

## Acceptance checks

- Manual source inspection: no DELETE/permanentDelete handler/transport path, and no retained evidence deletion.
- Executable focused check under root: `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainedMailFolderMoveTests|FullyQualifiedName~ApprovedOutlookCategoryTests"` must exit 0.

1. Table-driven Core cases cover eight action kinds, Staff-only PerformCasework,
   forged/disabled category, unapproved/mismatched state and absent restore
   origin; no category/destination string reaches policy as trusted input.
2. Existing fake HTTP proves exact immutable identity and scoped paths; read/
   category/flag PATCH confines properties; unrelated categories survive; 412
   never retries; Delete/Restore use move and no permanent-delete call exists.
3. Existing SQL harness proves same-key replay and changed-input conflict, two
   keys including ordinary move versus state action share one active claim,
   timeout/cancellation/process-reconstruction preserve unknown/no duplicate,
   and failed recovery remains occupied. Source/attachments/receipt/hash/Case
   links/Sent evidence are byte/value unchanged. Delete→restore returns the
   same row to effective Inbox/search/counts without allocation.
4. One actual Web cohort proves route/antiforgery/authority/current-version,
   confirmation Cancel does nothing, disabled composition absence, visible
   stale/failure/unknown and correct original-key Check status. Reuse the known
   MessageDetailShowsTheBodyAttachmentsThreadOutcomeAndTheWayBack capture for
   the default route plus only new states actually changed.
5. Exact migration/grants/bootstrap/role coverage; current snapshots and scope
   catalogue pass. Local success is not Graph enforcement, activation or live
   delivery. MAIL-028 must prove stale conditional update enforcement and the
   exact-target reversible journey before enabling production writes.

## Commands

Run only when scheduled by root, from the recorded future ticket worktree in
PowerShell 7 on this host. Existing genuine fixtures/fake Graph, no real mailbox.
The author returns exact changed/new test names before root chooses the run.

- Locked restore/build: existing solution runbook commands, or root's focused
  IntegrationTests project form; one build shared with these tests, no duplicate
  solution/CI loop.
- Core filter: `FullyQualifiedName~RetainedMailFolderMoveTests|FullyQualifiedName~ApprovedOutlookCategoryTests`.
- Integration filter for new tests named with `MessageState`, plus the existing
  `ConfirmedFolderMoveIsDurableReplayableAndPreservesArrivalEvidence`,
  `ConcurrentDifferentKeysHaveOneActiveClaimAndOneProviderMove`,
  `AuthenticatedUncertainMoveReusesTheSameConfirmationForExactRecovery`,
  `FolderMoveUsesExactScopedPostAndImmutableIdHeader`, actual role/migration
  cases touched, and default `MessageDetailShowsTheBodyAttachmentsThreadOutcomeAndTheWayBack`.
  Use `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  --configuration Release --no-build --filter "<the agreed exact filter>"`
  with the existing TRX/artifact retention options. This placeholder is a
  scheduling boundary, not a claim that a command was run.
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope inbox-message -SkipCapture`
  after the matching runtime capture exists; then the same command with
  `-Verify -SkipCapture -Scope inbox-message`.
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1`, existing migration
  grant validation and `git diff --check`.

No generic full suite, benchmark, new corpus campaign, browser download, live
Graph, consent, cloud deployment or repeated unchanged CI is scheduled here.

## Failure and deviation rules

Stop on an unsafe provider precondition, unsupported exact identity, missing
schema permission/census, failed test, conflicting ownership, new package/store
service or required files outside this map. Record actual error/output and
propose a bounded correction before proceeding. Never reduce assertions,
turn unknown into successful mutation, send an unconditional replacement PATCH,
or remove a retained-source record to make a test pass.

## Stop condition

For this request: write/read back current research/files/questions/plan/checklist
versions and return to root for approval. TICK-054 stays Preparing and unclaimed.
A future explicitly assigned author obtains fresh gates/packet/worktree and
stops after root verification plus report for independent review. No take,
source change, test, mailbox/cloud operation, merge or deployment is authorised
by this planning-only handoff. MAIL-028 activation and MAIL-031 Administration
remain their own records; TICK-088 is not included.
