# Post-implementation report — KANMER-005

## What shipped

The case edit lease now identifies its holder as `(ActorKind, SubjectId)`,
the identity `ActionActor` already owns, and every path that asks "is this
actor the holder" asks Core's one matcher, `CaseEditAuthority.IsHolder`.

- `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` — `IsHolder`;
  `RequireLease(…, ActionActor actor, …, ActorKind? retainedLeaseHolderKind, …)`;
  `IDescribeCaseEditAuthorityHolder.ExecuteAsync(ActorKind? holderKind, …)`
  discloses Automation from the retained kind, never from subject shape.
- `src/Pegasus.Core/Cases/CaseQueries.cs` — `CaseEditLeaseSnapshot.HolderKind`.
- `src/Pegasus.Infrastructure/Persistence/…` — `EditLeaseHolderKind` column
  (entity, model, migration `20260828110108_CaseEditLeaseHolderKind`, snapshot);
  claim writes it, `CaseMutationGuard.ClearLease` clears it,
  `CaseMutationGuard.RetainedHolderKind` parses it for the guard, the replay
  read, and both projections.
- `src/Pegasus.Web/Pages/…` — `CaseMutationPageModel.RestoreLeaseState`,
  Cases/Details, Cases/Assessment, Triage/Details use the matcher and pass
  the kind to the descriptor. No copy or control changed.
- MCP tools unchanged; they already reach the shared owner and map
  `CaseEditLeaseConflictException` to the existing refusal text.

Commits on `task/kanmer-005-lease-exclusivity` from `origin/dev` `1f2cf4a6`:
`2ab02db3` fix, `4a91c5c1` tests, `8218b3f3` simplification.

## Root cause

The store retained only the holder's subject (`EfCaseWorkflowStore.cs:177`)
and Core compared only the subject (`CaseEditAuthority.cs:68`); the replay
read (`:1244`) and four Web self-holder checks repeated that rule; the
descriptor inferred "Automation" from "subject is not a GUID". The claim
path's actor-agnostic `IsHeld` refusal has been in place since `012b3864`
(2026-08-05), before the reported 2026-08-18 event, so a claim-time
overwrite is not reproducible on dev and the retained identity gap is the
defect fixed. The reported end state (Automation's `edit_end` refused after
staff became holder) is consistent with the settled save-clears-lease
lifecycle: Automation saved, the lease cleared, staff claimed the free lease.

## Migration and rollout

One additive nullable column; `Down` drops it. No backfill, default or check
constraint: the old Web revision keeps writing null kinds until the new
package activates, and the new runtime treats a kind-less unexpired lease as
nobody's (unclaimable via `IsHeld`, unusable via `IsHolder`) until it
expires within five minutes. Production census on 2026-08-28: zero retained
holders. Existing table-level grants cover the column (research §Azure).

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0 (final tree).
- `dotnet ef migrations add CaseEditLeaseHolderKind …` — generated;
  reviewed as a single `AddColumn`; BOMs stripped to match sibling files.
- Tests: **not run by the implementer** by instruction; the EPIC-011
  orchestrator runs the wave loop. New/changed tests:
  `tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs`;
  `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` (three
  SqlServer tests + `ReadLeaseRowAsync`); `CaseDetailsWebTests.cs` (one
  test; fake gains `LeaseHolderKind` and refuses a claim on `NextFailure`);
  `AutomationAssessmentIngressTests.cs` (two real-HTTP tests);
  `OperationsWebTests.cs`, `CaseReportApprovalWebTests.cs` fakes;
  `IntakePersistenceIntegrationTests.cs` census.

Ticket verification bullets → tests:

| Bullet | Test |
| --- | --- |
| Automation holds → staff cannot claim or edit | `AnAutomationHeldLeaseRefusesEveryStaffOperationAndTheHolderStillSaves`, `AnAutomationHeldCaseIsReadOnlyToStaffAndAPostedClaimIsRefused`, `AnAutomationHeldLeaseRefusesTheStaffClaimAndLeavesTheWorkspaceReadOnly` |
| Staff holds → Automation cannot claim or edit | `AStaffHeldLeaseRefusesEveryAutomationOperationAndTheHolderStillReleases`, `AStaffHeldLeaseRefusesAutomationBeginWriteAndEndOverHttp` |
| Holder saves / releases after a competing attempt | the two persistence tests above (save consumes; release without save) and the ingress tests (holder ends) |
| Ownership unchanged after rejected claim or write | `ReadLeaseRowAsync` equality in all three persistence tests; `EditLeaseHolderKind`/`EditLeaseHolder` SQL reads in the ingress tests |

## Risks and limitations

- The ingress test `AnAutomationHeldLeaseRefusesTheStaffClaimAndLeavesTheWorkspaceReadOnly`
  hosts the staff test identity and the MCP bearer scheme in one factory
  (`useIntegrationTestAuthentication: true` + `WithAutomationMcp`). The MCP
  endpoint policy names its own scheme, so this should coexist; if the
  orchestrator's run shows the token endpoint rejecting under that factory,
  the staff GET can move to a second factory over the same database.
- The historical incident cannot be reconstructed (research §Azure); tests
  prove the invariant directly.
- Wave-3 rule "one unmerged migration at a time": this PR carries one
  migration; TICK-061 is next in the recorded order.

## Out of scope / follow-ups

- Details/Assessment evaluate the holder match twice (pre-existing CASE-024
  shape) — for the Case lanes.
- Structured lease telemetry — separate observability scope.
