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
| `src/Pegasus.Infrastructure/Persistence/Migrations/` | Add one migration and update the EF model snapshot for the existing workflow table. The production census found no retained holder rows, so no fabricated holder-kind backfill is needed; nullable inactive rows remain inactive. |
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
  Lease claim must set kind; every clear path must clear it; consistency checks
  must reject partial holder identity rather than guessing.
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
