# Files — TICK-054 recoverable exact-message actions

Current map at dev `493f7460d7728a6576d240d4feb7d0bf2a377ec5`.
Supersedes file-map version `45f10bfc5527632f`; old gated versions remain in
board history. This is not authority to take a worktree or modify source yet.

## Where the change lands

| Path | Responsibility / risk |
| --- | --- |
| `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs` | Extend the already-registered MoveRetainedMailFolder owner with concrete state-action, refresh and check-status methods plus existing store/transport contracts; no additional command class. Ordinary designated moves retain their classification rule; new actions cannot forge it. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Expose separately labelled arrival and observed Outlook state, freshness, current effective scope and allowed exact-message actions. |
| `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs` | Add a staff-authorized active-choice method on the existing registered ListApprovedOutlookCategories query, using the existing store/resolver and single catalogue; keep its admin method unchanged. No new administration or master-category sync. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` | Existing journal owner carries all these concrete actions, shared per-message exclusion, replay/recovery, current observation and recorded restore target. No second move store or workflow. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Extend existing operation row with closed action kind/expected-state/before-after evidence; add one per-retained-message mutable Outlook-state projection without changing arrival fields. |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Journal constraints and unique pending/uncertain exclusion; current-state row FK/version and no cascading evidence deletion. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Single effective unread/current-folder projection for detail, list/counts and retained search; restored Inbox items reappear without re-intake. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Reuse GraphMailClient and GraphRetainedMailFolderMover; exact metadata read/conditional property PATCH and existing move/Deleted Items resolver. No DELETE/permanentDelete endpoint. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/*RetainedMailMessageState*.cs` | One normal migration and generated designer only, current-state projection plus narrow journal changes; exact Web grants/DELETE denial in same diff. No historic migration edits. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Generated current EF model only. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Same-migration grant census and least-privilege matrix, no broad grants or execution. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Existing exact-message toolbar/detail and reason dialog pattern; actions only here. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Thin authenticated/antiforgery action and read-only reconciliation handlers with server-resolved identity/state; preserve originating list context. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | One label vocabulary for named actions/states. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs` | Closed action/actor/destination/category and expected-state policy cases. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Adapt the existing FolderMoveState double to the extended ports; preserve suggestion-view no-probe/no-mutation assertions. |
| `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs` | Staff active choices and disabled/forged category refusal. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Existing SQL harness: shared exclusion/replay/restart, evidence preservation, restore target and effective list/counts. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Existing fake HTTP: exact identities/headers/property confinement, 412 and uncertain responses, read-only recovery. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Existing authenticated real-page harness: controls, antiforgery/version/refusal/recovery and minimal actual route captures. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Prove actual Web owner can execute its state/journal writes; retained evidence DELETE remains refused and Worker does not acquire a mail mutation grant. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` | Update the known exact pending-migration inventory when the new migration is generated; do not repeat DOCS-020's omitted-list failure. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Clarify settled MAIL-13 action/current-state/restore/recovery semantics; retain read-only browsing and permanent-deletion prohibition. |
| `docs/capabilities.md` | State implementation vs activation accurately, not Done from registration. |
| `docs/current-architecture.md` | Describe the actual shared owner and explicit still-closed activation state after implementation. |
| `docs/design/test-ui/pages/inbox-message--*.html` | Generated captures owned by the changed route, only measured states. |
| `docs/design/test-ui/index.html` | Generated capture metadata if changed by the existing update script. |

## Context files

| Read, do not modify for this scope | What it establishes |
| --- | --- |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Existing move/store/category owners are registered; normal constructor injection supports the bounded extension. No registration, service-locator or activation change. |
| `docs/operator-notes.md`, `docs/frd/frd-04-parties-accounts-and-access.md`, `docs/adr/0004-provider-api-and-staff-mcp-authentication.md` | Binding no-permanent-delete rule and staff role ownership; no meaning change authorised. |
| `docs/design/README.md`, `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | Existing confirmation, focus, status/error, no explanatory-copy and responsive conventions. Reuse without changing the shared component. |
| `src/Pegasus.Core/Intake/Classification/MailLogicalFolderPolicy.cs` | Business folder taxonomy is not Inbox/Deleted Items; preserve classification policy. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | Exact approved mailbox and folder bindings/version. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs` | Existing canonical active-name resolver and admin history; no new catalogue. |
| `src/Pegasus.Core/Operations/StaffMailSend.cs` | Existing sending is a separate owner; no scope expansion into TICK-088/MAIL-027 send. |
| `docs/runbook.md`, `docs/operations.md` | Local/fake vs exact-target live activation evidence, root verification ownership. No deployment or permission mutation in this plan. |
| `scripts/Update-TestUiSnapshots.ps1` | Use one actual routed capture cohort and focused scope; missing capture is not a successful visual check. |

## Ripple effects and exclusions

TICK-049 and MAIL-004 are integrated prerequisites. MAIL-028 is the separate
real-adapter activation owner; MAIL-031 is the downstream Administration owner.
MAIL-027's historically deferred flag/delete overlap is consumed by this
settled TICK-054 boundary, not a second implementation. AUTO-003 remains a
later thin Automation caller, not part of this staff-only change.

Do not modify Worker scheduling/intake/cutoffs, MAIL-031 administration,
TICK-088 compose/send, migration history, credential/configuration activation,
Graph permissions, shared CSS/JS, the folder taxonomy, corpus/reference data,
PLAT-075's branch/worktree/claim, or source outside the declared map. Changes
outside the map require a refreshed approved plan first.


## Current-base and ownership reconciliation

Research `05570adfb75900ee` records the six changed mapped paths since
dev19e6f523. Preserve current principal-routing query/test names, INTK-063's
actual Worker test and as-built recovery description, and the accepted
generated index. No existing mover/category/Graph/page contract changed.

The two explicit EF move-store constructions are in
RetainedMailPersistenceTests and stay mapped fixture adaptations. The omitted
RetainedMailTests port double is now mapped; no new test support is needed.
Keep AzureSqlRuntimeRoleMigrationTests for its existing exact mail grants and
restricted-connection caller pattern. It and current-architecture.md remain
whole-file coordination points with INTK-064, not removed or relocated for
parallelism. Root must order ownership before edits. MAIL-028's conditional
PATCH canary remains separate, unproved activation evidence.
