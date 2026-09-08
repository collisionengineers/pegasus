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

## Operator-approved verification-gate removal — 8 September 2026

The operator explicitly instructed “remove the test” after reviewing the unsupported corpus-wide percentage gate, then approved the prerequisite execution-log split. Delete only `tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs`, including its arbitrary 60% field-presence assertions and test-only CSV helper. This is the explicit exception to the earlier no-unrelated-baseline-repair boundary. Retain all genuine exact-value extraction, routing, classification, upload confirmation and replay tests unchanged by this removal. Do not lower thresholds, exclude the test with filters, change extraction policy, alter corpus files or claim its previous failure passed. Preserve the failure in execution history; rebuild and run remaining non-browser regression before handoff. No new package or production behavior is added by this deletion.

## Full-regression affected-consumer disposition — 8 September 2026

The completed full non-browser rail at 75f112a18 failed exactly five assertions (1,969 other tests passed). Explicitly name these direct manual-upload consumers within the existing affected integration-test wildcard scope:

- `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs`: only ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage. Assert current manual destination/proposal action and real unallocated receipt, retaining pending work, duplicate identity and legitimate Unidentified provenance.
- `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs`: only four tests expecting automatic allocation after genuine ManualUpload. Retain exact fields, source identities, hashes/assets/custody, conflicts and replay; assert no allocation/reference/link/sequence before acceptance and exact recorded-only history. Rename the typed-review test accordingly. No silent acceptance helper or conversion to Mailbox; no production-code changes.

The fifth class method for invalid/conflicting extraction remains unchanged. Existing Create/confirmation tests own mandatory acceptance and replay. This corrects the whole affected-consumer root class, not individual example workarounds. Root owns QdosIntakeWebTests; /root/fixture_corrections exclusively owns InstructionDraftWebTests under a fresh same-root assignment. No host tests/build/capture by either source author. Full regression failure remains evidence, not PASS; a fresh affected-class run must close each finding before snapshot/handoff.

## Operator revision — 9 September 2026

The operator explicitly orders removal of ALL Test UI snapshot/capture and Browser-category tests from local and CI and resubmission of this change. This supersedes every earlier capture/browser retention or verification requirement, the generated-only editing constraint, and the .github/docs/operations/Razor-skill exclusions only for this removal. Keep it in this requested PR; do not start an unrelated enhancement or replacement harness. Preserve all other implementation and evidence history.

Delete tests/Pegasus.IntegrationTests/Browser/**, tests/Pegasus.IntegrationTests/TestUi*.cs, scripts/Update-TestUiSnapshots.ps1, scripts/Test-UiCatalogue.ps1, scripts/Test-UiModes.ps1 and docs/design/test-ui/**. Remove capture startup hooks and capture-only helpers from HTTP test support and ReadinessEndpointTests; remove obsolete comments in affected tests. Remove Deque.AxeCore.Playwright and direct test Microsoft.Playwright references; regenerate affected NuGet lock data normally, preserving transitive Microsoft.Playwright needed by the actual report renderer and its runtime bootstrap/container packaging. Delete CI browser/test-ui jobs and catalogue step; remove obsolete Browser exclusion in SQL selection, retired path flags/regressions, UiMode switch and all callers. Update README.md, AGENTS.md unmanaged guidance, docs/index.md, docs/engineering.md, docs/runbook.md, docs/design/README.md, docs/frd/frd-12-operator-experience.md, docs/operations.md and .agents/skills/razor-pages-ui-implementation/SKILL.md for the removed system only. Check all tracked references; remove obsolete current instructions and repair links without rewriting unrelated domain snapshots or immutable supplied evidence. Archived Kanmer evidence remains truthful history, not active requirements.

Expected additional files: .github/workflows/ci.yml; scripts/Get-CiChangeFlags.ps1; scripts/Test-CiChangeFlags.ps1; scripts/Invoke-LocalDevelopment.ps1; scripts/Invoke-TestShard.ps1; scripts/Test-UiModes.ps1; scripts/Test-UiCatalogue.ps1; scripts/Update-TestUiSnapshots.ps1; tests/Pegasus.IntegrationTests/**; affected tests/**/packages.lock.json; README.md; AGENTS.md; docs/index.md; docs/engineering.md; docs/runbook.md; docs/design/**; docs/frd/frd-12-operator-experience.md; docs/operations.md; .agents/skills/razor-pages-ui-implementation/SKILL.md. Any further active caller/reference discovered is named before editing.

Acceptance: no browser tests or capture code/assets/jobs/modes remain locally or in CI; no replacement browser framework; nonbrowser unit/HTTP/SQL and report-renderer production behavior preserved. Validate Release build, focused affected HTTP cohorts, change-flag/sharding/local-script/parser/document checks and no dangling references, with frozen-source named final_verifier ownership. Existing long full-rail results retained honestly; do not run removed tests or claim visual approval. Stop with updated PR ready for independent review, no merge/deployment.

## Runtime installer affected callers

Additional exact allowed files: `scripts/Initialize-LocalDevelopment.ps1` and `scripts/Invoke-Doctor.ps1`. Point the Chromium installer at src/Pegasus.Infrastructure/bin because PDF generation is a production dependency, not a test dependency. Remove browser-test-only certificate commentary. Preserve actual runtime installation and checks.
