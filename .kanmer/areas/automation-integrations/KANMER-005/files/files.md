# Files — KANMER-005

*Refreshed 2026-08-28 against `origin/dev` at `1f2cf4a6` (CASE-024 merged).
Earlier revisions of this document were written against the pre-CASE-024
tree; this one replaces them.*

## Root cause (verified on dev, read-only)

The lease retains and compares only the holder's subject text. Core identity
is `ActionActor(Kind, SubjectId)`, but:

- `src/Pegasus.Core/Workflow/CaseEditAuthority.cs:68` — `RequireLease`
  compares `retainedLeaseHolder` to `actorSubjectId` only.
- `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs:177` —
  the claim retains `request.Actor.SubjectId` and nothing of its kind;
  `ReadLeaseReplayOrThrow` (`:1244`) compares subject only.
- `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs:151`,
  `Pages/Cases/Details.cshtml.cs:339`, `Pages/Cases/Assessment/Index.cshtml.cs:724`,
  `Pages/Triage/Details.cshtml.cs:490` — four copies of the same subject-only
  "viewer is the holder" rule.
- `CaseEditAuthority.cs:118` — `DescribeCaseEditAuthorityHolder` infers
  "Automation" from "subject is not a GUID".

The claim path itself (`EfCaseWorkflowStore.cs:165`, `IsHeld` before any
write, under the serializable row lock) has refused every unexpired lease
regardless of actor since 2026-08-05 (`012b3864`), which predates the
reported incident; a claim-time takeover is not reproducible on dev. The
enforceable defect is the incomplete holder identity above.

## Where the change lands

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` | One matcher `IsHolder(ActorKind? retainedKind, string? retainedHolder, ActionActor actor)`; `RequireLease` takes the `ActionActor` and the retained kind; the holder descriptor takes the retained kind and never parses subject shape. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseEditLeaseSnapshot` gains `ActorKind? HolderKind` (null only for a row written before the column existed). |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`, `CaseWorkflowModelConfiguration.cs` | Nullable `EditLeaseHolderKind` (nvarchar 40, same width as every other retained `ActorKind`). |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<ts>_CaseEditLeaseHolderKind.cs` (+ Designer, snapshot) | `AddColumn` only; `Down` drops it. |
| `src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs` | Parses the retained kind for Core, clears it with the rest of the tuple. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | Claim writes the kind; replay compares through the Core matcher. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`, `EfOperationsStore.cs` | Project the retained kind into the snapshot. |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs`, `Pages/Cases/Details.cshtml.cs`, `Pages/Cases/Assessment/Index.cshtml.cs`, `Pages/Triage/Details.cshtml.cs` | Replace the four subject-only checks with `CaseEditAuthority.IsHolder`; pass the kind to the descriptor. No copy change. |
| `tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs` | Kind-aware matcher, null/unknown kind, descriptor by kind. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` | Both actor directions: claim/write/renew/release refused, state unchanged, holder continues (save and release-without-save), same-subject/different-kind with the live token. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` (+ `OperationsWebTests.cs` fake) | Automation-held workspace: read-only, no claim form, staff claim POST refused; fake signatures follow the contract. |
| `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | Real HTTP: staff holds → `pegasus_case_edit_begin`/`pegasus_assessment_update` refused with the existing conflict mapping; automation holds → staff claim refused and workspace read-only. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Committed-migration census gains the new migration. |

## Context files

`src/Pegasus.Core/Identity/IdentityContracts.cs` (`ActionActor`, `ActorKind`);
`src/Pegasus.Core/Lifecycle/CaseCommandSeams.cs` and
`src/Pegasus.Infrastructure/DependencyInjection.cs:277` (staff and MCP share
`ILeaseCaseForEdit` → `EfCaseWorkflowStore`); `src/Pegasus.Web/Mcp/CaseMcpTools.cs`,
`AssessmentMcpTools.cs`, `AutomationMcpErrors.cs` (existing conflict mapping,
unchanged); `Pages/EditModeDisplay.cs` (existing "Case locked - AI is editing"
copy, unchanged); `Pages/Shared/_EditHeartbeat.cshtml` (CASE-024; unchanged).

## Out of scope

Save-clears-lease, lease duration, heartbeat interval, the lock/isolation
shape, MCP result schemas, any takeover or force path, a backfill or check
constraint on the new column (see plan), governing-doc edits.
