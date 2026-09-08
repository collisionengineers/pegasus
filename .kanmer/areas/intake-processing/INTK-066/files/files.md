# Files — INTK-066

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Core/Intake/DurableIntake.cs | Withhold manual unique-match automatic association; reuse link owner. |
| src/Pegasus.Core/Intake/IntakeAllocation.cs | No automatic ManualUpload allocation or Case/PO. |
| src/Pegasus.Core/Intake/IntakeContracts.cs | Bounded confirmation/replay read contract if needed from existing persisted data. |
| src/Pegasus.Core/Intake/IntakeAssociationDestinations.cs | Core receipt/destination query contract and shared current viability policy. |
| src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs | Manual images stay pending through automatic recovery until reasoned staff association. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeAssociationDestinations.cs | Receipt-scoped viable Case reads from real retained data. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs | Share authoritative eligibility; preserve atomic version/lease/history/replay semantics. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs | Map existing association identity for truthful same-decision retries if required. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | Register actual query port caller. |
| src/Pegasus.Web/Presentation/UploadOutcome.cs | Truthful proposed/settled outcomes and viable choices. |
| src/Pegasus.Web/Presentation/UploadCaseDecision.cs | Bound search/confirmation, no silent partial success, typed errors and safe retries. |
| src/Pegasus.Web/Pages/UploadConfirmationPageModel.cs | Route/member, antiforgery, bound input and errors. |
| src/Pegasus.Web/Pages/UploadStatus.cshtml.cs | Single-route context, versions, proposals, error re-render. |
| src/Pegasus.Web/Pages/UploadStatus.cshtml | Current decision and errors. |
| src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs | Group membership and one coherent decision over current members. |
| src/Pegasus.Web/Pages/UploadGroupStatus.cshtml | Group viable options and error state. |
| src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml | Suggested viable destinations, search, confirmation and reason. |
| src/Pegasus.Web/Pages/Cases/Create.cshtml.cs | Reuse extracted-details acceptance and retained classified Audit evidence. |
| src/Pegasus.Web/Pages/Cases/Create.cshtml | Explicit proposal acceptance/rejection, no Case/PO preview. |
| src/Pegasus.Web/Program.cs | Affected composition only if constructor/registration changes. |
| src/Pegasus.Web/wwwroot/js/site.js | Receipt-scoped suggestions, selected version, stale/error behavior. |
| src/Pegasus.Web/wwwroot/css/site.css | Only necessary current-component option/error presentation. |
| tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs | Manual proposal vs unchanged non-manual allocation. |
| tests/Pegasus.Core.Tests/Intake/IntakeAssociationDestinationTests.cs | Shared viability rules. |
| tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs | Manual pending first/replay/sweep and confirmed completion. |
| tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs | Affected automatic channel expectations. |
| tests/Pegasus.IntegrationTests/*.cs | Only direct affected upload/create/image/allocation tests and genuine shared seed consumers; no unrelated correction. |
| tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs | Real browser confirmation, search, keyboard/error and responsive evidence. |
| tests/Pegasus.IntegrationTests/Browser/QdosAllocationRecoveryBrowserTests.cs | Only affected proposal/accepted seed contract if required. |
| tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs | Only affected pending decision/polling contract. |
| docs/frd/frd-02-intake-and-source-identity.md | Settled manual confirmation and allocation exception. |
| docs/frd/frd-12-operator-experience.md | Upload interaction agrees with FRD-02. |
| docs/design/test-ui/pages/upload-status--*.html | Affected generated route snapshots. |
| docs/design/test-ui/pages/upload-group-status--*.html | Affected generated route snapshots. |
| docs/design/test-ui/pages/case-create--*.html | Affected generated proposal snapshots, verify catalogue name first. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| AGENTS.md | One Core policy owner, host-slot serialization, foreign edits and no live grants. |
| docs/index.md | Canonical documentation boundaries, no new status ledger. |
| docs/design/README.md | Existing tokens, components, keyboard, 1580/1100/760 review and no invented UI system. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Reasoned permanent Case/PO, current lease and lifecycle authority; deferred full-v1 implementation stays separate. |
| docs/frd/frd-04-parties-accounts-and-access.md | Existing staff permissions; no route-only access control. |
| src/Pegasus.Core/Workflow/CaseEditAuthority.cs | Expected version, live same-actor lease, no theft/bypass. |
| src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs | Transaction-time edit/state/archive enforcement. |
| src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs | U-reference resolution/recovery is not a second workflow. |
| src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs | Existing image eligibility/registration/merge policy. |
| tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs | One genuine intake driver and seed conventions. |
| scripts/Update-TestUiSnapshots.ps1 | Scoped capture, existing appended focused cohort and retained evidence behavior. |

## Ripple effects

Manual upload expectations must change where they intentionally test that route.
Other tests needing accepted Cases must use explicit accepted-case setup, never a
hidden auto-confirm in generic UploadAsync/ProcessQueuedAsync helpers.
No new dependency, DDL, grants, runtime asset package or release operation.

## Out of scope

Mailbox/provider automatic policy, arbitrary manual Audit creation, unresolved
full-v1 Audit/Completed/Query changes, public session/capacity tickets, Principal
document moves, alert/cloud changes, production reset/promotion/deployment.

## Necessary affected-consumer disposition — 8 September 2026

Static caller inspection found two consumers not named in the original map. These are necessary consequences of changing the shared upload confirmation contract, not unrelated cleanup. Add only:

| Path | Why |
| --- | --- |
| src/Pegasus.Web/Pages/Mail/Message.cshtml.cs | Retain its supported generic Case search through existing ISearchCases, trimming/minimum-length/bounded result semantics; no upload-specific query dependency. |
| src/Pegasus.Web/Pages/Cases/Index.cshtml.cs | Preserve existing Awaiting instruction image attach handler through new bound shared confirmation contract; selected-image receipt scope, reviewed versions and retry/error rendering. |
| src/Pegasus.Web/Pages/Cases/Index.cshtml | Update its existing Attach form to the same explicit confirmation/reviewed-input contract; do not remove supported capability. |
| tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs | Prove the existing Awaiting image attach path remains wired and properly scoped. |
| tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs | Relevant unchanged generic Case-search behavior if required by direct caller diff. |
| docs/design/test-ui/pages/cases-index--*.html | Affected queue snapshots only; resolve exact catalogue scope before capture. |

The existing Awaiting image queue supports explicit staff association for registered images from Mail/Provider as well as ManualUpload. CanOffer may therefore accept ImageIntakeRegistered across channels, while ordinary receipt eligibility and all new automatic ManualUpload guards stay scoped as planned. Core owns this single eligibility rule; no fallback or duplicate policy.

## Resolved catalogue path — 8 September 2026

Catalogue inspection resolves the prior provisional cases-index snapshot entry: `src/Pegasus.Web/Pages/Cases/Index.cshtml` at `/Cases` owns `docs/design/test-ui/pages/queues--default.html` and `queues--empty.html`. The authorized generated-file scope is `docs/design/test-ui/pages/queues--*.html`, replacing the nonexistent `cases-index--*.html` placeholder. `cases--*.html` belongs to `/Search` and is not added. Capture scopes are upload-status, upload-group-status, case-create and queues, using their actual owning cohorts. This changes no product scope.
