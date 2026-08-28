# Files — KANMER-005

*This maps the implementation surface for complete cross-actor lease identity
and the regression evidence around the existing shared lease.*

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` | Compare holder kind and subject as one identity for write, renew, and release authorization; describe the holder from recorded kind instead of GUID shape. Preserve token, expiry, and version checks. |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | Add the holder kind to the live `CaseEditLease` returned by claim and renew. Constructor changes ripple to callers and test fakes. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Add holder kind to `CaseEditLeaseSnapshot` so Web and operations projections do not have to infer actor type from subject text. |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs` and `CaseWorkflowModelConfiguration.cs` | Persist the live holder kind beside `EditLeaseHolder` and enforce the same null/non-null lifecycle for the holder identity. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` and `CaseMutationGuard.cs` | Write, replay, require, renew, release, and clear the complete holder identity while retaining the existing serializable workflow-row lock and claim ordering. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` and `EfOperationsStore.cs` | Project the recorded holder kind into case and operations views, including active-lease consistency checks. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/` | Add one migration and update the EF model snapshot for the existing workflow table. Before enforcing holder-kind consistency, clear any complete ephemeral lease tuple that exists; actor kind cannot be reconstructed safely from subject text. The research-time census found none, but the migration must remain safe if a lease exists later. |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` and the Details/Assessment edit-mode models after [[CASE-024]] | Restore or display edit ownership only when actor kind and subject both match, and keep a competing holder's page read-only with no claim action. Reuse CASE-024's shared handlers. |
| `tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs` | Pin same-subject/different-kind refusal and kind-driven holder description. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` | Add the real-store staff/Automation claim, renew, release, write-refusal, state-preservation, expiry, and synchronized-race matrix. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` and `CaseEditModeWebTests.cs` | Prove an Automation-held case is read-only to staff, renders no claim action, and uses the CASE-024 shared handler behavior. |
| `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | Exercise a real Automation caller against a staff-held case and the holder's successful continuation after the rejected attempt. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Core/Identity/IdentityContracts.cs` | `ActionActor` identity already consists of `ActorKind` plus `SubjectId`; reuse it rather than introducing a second identity type. |
| `src/Pegasus.Core/Lifecycle/CaseCommandSeams.cs` | Staff and Automation already use the same claim, renew, and release ports; no new actor-specific port is warranted. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | All production lease commands already resolve to `EfCaseWorkflowStore`. |
| `src/Pegasus.Web/Mcp/AutomationActorResolver.cs` and `infra/modules/platform.bicep` | Automation is constructed with its explicit kind; production currently supplies the non-GUID subject `pegasus-automation`, which explains why the identity gap does not reconstruct the incident. |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | One server-owned lease, no takeover, and the same guard for Web and Automation are already normative. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Automation writes must use the same Core commands, lease, and version guard as staff writes. |
| `docs/adr/0011-restrict-mcp-to-automation-actor.md` and `docs/adr/0031-automation-actor-contract-without-eva-export-tools.md` | Automation is a distinct durable actor, not a staff impersonation, within the existing application boundary. |
| Kanmer [[CASE-024]] research, plan, and PR 581 | Successful save clears the lease; heartbeat cannot resurrect it; shared Details and Assessment claim/release handlers must land before this ticket edits them. |
| Holder-authenticated mutation stores calling `CaseMutationGuard.Complete` | Save-clears-lease is system-wide. Background writes also invalidate leases, so this ticket must not change `Complete` or selectively retain leases. |

## Ripple effects

- Adding holder kind to the two Core lease records updates query projections,
  Web display helpers, MCP responses where the lease result is serialized, and
  test fakes that construct those records.
- The migration, model configuration, and model snapshot must ship together.
  It clears any pre-existing ephemeral lease tuple rather than fabricating a
  holder kind. Lease claim must set kind; every later clear path must clear it;
  consistency checks must reject partial holder identity rather than guessing.
- Rejected cross-actor claims and writes must be proved to leave holder kind,
  subject, token, request/operation keys, expiry, version, and concurrency state
  unchanged.
- The two holder-continuation checks are separate: save succeeds and ends the
  lease, or release succeeds without a preceding save. A stale `edit_end` after
  save remains an expected refusal.
- No governing-document edit is expected because FRD-01, FRD-11, and ADR-0011
  already require the behavior. If implementation reveals a behavior change
  rather than enforcement of those rules, stop and route it through
  `kanmer-docs`.
- Canonical restore, Release build, and non-Corpus tests remain the delivery
  gate. Focused Core and integration suites should run before the full gate.

## Out of scope

- Changing the rule that a successful mutation ends the lease.
- Changing the five-minute lease duration, CASE-024 heartbeat interval, or
  Assessment edit-mode design.
- Adding any staff or Automation takeover, force-release, or actor-specific
  lease implementation.
- Altering background-mutation lease invalidation.
- Merging or redesigning [[CASE-024]]; KANMER-005 waits for it and consumes its
  shared page convention.
- Deployment, production data mutation, package additions, or a new store,
  project, runtime, or architecture boundary.

## Microsoft/Azure refinement — 2026-08-28

This section supersedes the earlier migration row and adds the rollout,
dependency, and verification surfaces discovered by first-party guidance and
read-only production checks.

### Refined implementation surface

| Path | Refined responsibility |
| --- | --- |
| `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` | Accept the typed holder kind with the subject and token; centralize exact actor identity matching; disclose Automation from the stored kind and represent a missing/unsupported rollout kind as unnamed rather than parsing GUID shape. |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | Add non-null `ActorKind HolderKind` to newly issued/replayed `CaseEditLease` values, including CASE-024 heartbeat returns. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Carry nullable `ActorKind? HolderKind` in the active snapshot so a migration-window partial identity remains visible and fail-closed without pretending to be Staff or Automation. |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs` and `CaseWorkflowModelConfiguration.cs` | Add nullable, length-40 `EditLeaseHolderKind` with no default. Do not add the paired-null database constraint in this release because the old Web writer remains live between migration and package activation. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | Under the existing short serializable row lock, set kind on claim, preserve it on renew/heartbeat, compare it on replay/release, return it in lease results, and keep an incomplete active tuple unavailable until expiry. Do not add retry or another lock. |
| `src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs` | Parse the retained kind case-sensitively, pass complete identity to Core, and clear kind on every mutation/lease clear. Missing or unknown kind cannot authorize a write. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` and `EfOperationsStore.cs` | Select and project the kind with the holder. Include it in lease consistency checks while preserving an active incomplete tuple as unavailable/unknown rather than dropping it and rendering a claim path. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseEditLeaseActorIdentity.cs`, its generated Designer, and `PegasusDbContextModelSnapshot.cs` | Add the nullable column; backfill only from an exact matching claim/renew operation row; clear only unmatched ephemeral tuples; provide the column-only `Down`; keep the generated artifacts together. |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs`, `Pages/Cases/Details.cshtml.cs`, and `Pages/Cases/Assessment/Index.cshtml.cs` after [[CASE-024]] | Reuse the merged shared claim/release/heartbeat handlers and compare holder kind plus subject when restoring edit state or deciding whether the viewer holds the lease. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs` | Pass recorded kind to the shared holder descriptor and use full actor identity for its linked-Case held-state rendering; its claim still goes through the shared Core/store path. |
| `tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs` | Prove same subject with different kinds is not the same holder, kind-driven display, and missing/unsupported persisted kind fails closed. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` | Add both Staff/Automation directions, exact retained-state assertions, renew/release/save continuation, separate-context synchronized claims with one winner, replay identity checks, and partial/unknown-kind recovery. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` | Migrate from the immediate predecessor with exact-match, unmatched, and no-holder old-schema rows; assert the backfill/clear result, nullable column, reversible schema shape, and no pending migration. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Add the generated migration to the exact ordered committed-migration census. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`, `CaseEditModeWebTests.cs`, and `AutomationAssessmentIngressTests.cs` | Prove Automation-held UI is read-only/no claim, Staff-held Automation begin/write is refused, the holder can continue, and save still consumes the lease. Extend CASE-024's merged heartbeat/edit-mode tests. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Inspect/run the existing object-level grant census; change it only if the generated migration actually changes permissions. Existing table grants already cover the new column. |

### Migration and release rules

- Generate the migration only after [[CASE-024]] merges and the ticket branch is
  based on the then-current `origin/dev`. Re-read the immediate predecessor
  rather than baking today's latest migration into SQL or test names.
- The exact history backfill joins `CaseId`, `EditLeaseOperationKey`, holder
  subject, claim/renew operation kind, result version, token hash, and expiry.
  It copies the recorded operation `ActorKind`; it never parses subject shape
  or duplicates the `ActorKind` enum as a SQL allow-list.
- A retained holder with no exact operation row has its token, token hash,
  request hash, holder, holder kind, operation key, and expiry cleared
  together. That transient clear is the only irreversible transformation and
  loses no Case data or history.
- Do not add a default or trusted paired-null check constraint in this release.
  The nullable column keeps the migration compatible with the old Web writer
  during migration-before-activation. New code fails closed on a partial kind,
  and an expired partial tuple uses the existing atomic clear/reclaim path.
- No GRANT statement is needed. Production readback confirms the Web and
  Worker object-level `CaseWorkflows` permissions already cover every column.
  Preserve the current `DELETE` denies and do not alter role membership.

### Dependency and evidence limits

- [[CASE-024]] is now a typed blocker. PR 581 is open at `747ecc47` with green
  CI but unresolved P1 review findings and operator sign-offs. KANMER-005 must
  not take a worktree or edit the pre-merge page/heartbeat copies.
- Azure platform metrics and current SQL do not reproduce the reported
  takeover. The incident-window platform was available, but application logs
  were absent and current lease/history rows are empty. Tests must therefore
  prove the invariant directly; do not claim a reconstructed root event.
- AppLens/Resource Health are not usable evidence for the current Container
  App/SQL topology through the connected tools. No provider registration,
  Azure resource change, deployment, logging expansion, or alert creation is
  part of this ticket.
- No MCP response-schema expansion is required. The production MCP claim and
  write callers must exercise the corrected internal identity guard; exposing
  a new external holder-kind field would be separate contract scope.
