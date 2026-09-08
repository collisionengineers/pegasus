# Plan — INTK-066: manual upload destination confirmation

## Objective
Manual uploads retain extracted source material pending an explicit staff decision:
confirm a viable existing Case, or accept/reject an editable new-Case proposal.
Never associate automatically or propose/reserve Case/PO before acceptance.

## Starting state
Integration baseline origin/dev 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c.
Evidence: `research/research.md`@`16702074ba67f26c`, `files/files.md`@`e4a0f08f02f5ca13`.
Preparing feature ticket, untaken; no source modifications or verification yet.
The operator's 8 September implementation instruction and subsequent new-Case
clarification approve this bounded behavior and necessary FRD reconciliation.
Shared checkout contains foreign Principal-document edits; do not use it for implementation.

## Governing docs
Modifies docs/frd/frd-02-intake-and-source-identity.md under explicit operator
authorization: ManualUpload is not the automatic destination/allocation route,
even with one match. Reconcile conflicting matching/Unidentified/upload clauses.
Modifies affected Upload clauses of docs/frd/frd-12-operator-experience.md to agree.
Meets current FRD-01 permanent reference, staff lease/version and lifecycle rules,
and FRD-04 staff permissions. Deferred full-v1 lifecycle/Audit requirements are not
an invitation to redesign them. No arbitrary hand-keyed Audit creation.

## Required changes
- Withhold ManualUpload automatic destination at ProcessQueuedIntake unique match,
  AllocateIntake.AttemptAutomaticAsync and ImageIntakeCasePairing first/recovery
  entry points. Preserve extraction, original-byte custody, image identity,
  mailbox/provider automation and post-confirmation processing.
- Add the small Core IIntakeAssociationDestinationQueries contract and persistence
  adapter. Its production callers are UploadOutcomeQueries and UploadCaseDecision.
  Reuse existing matching inputs for initial candidate suggestions, and current
  search semantics for explicitly entered search; filter both to receipt-scoped
  viable targets. One candidate is one suggestion, never auto-selection as consent.
  Group suggestions must be viable for every member actually awaiting this decision.
- Centralize current viability facts/rules in Core, reusing current Case lifecycle
  and registered-image lifecycle owners; call the same rule in read and transaction
  write paths. Enforce supported receipt integrity/readiness and reject unsafe,
  unsupported or still-processing material, archived/terminal Cases and image
  destinations after report-sent. Do not invent Principal-equality restrictions or
  conflate a temporary lease conflict with permanent Case eligibility.
- Reuse ILinkIntake, existing edit lease, receipt/Case expected versions and immutable
  history. Bind search and POST to the real single upload route or server-loaded
  group membership and ManualUpload channel; never trust arbitrary posted receipt ids.
  Retain antiforgery and staff authorization, including before any replay shortcut.
  Require reason (nonblank, at most 500 chars), valid page operation id, reviewed
  receipt versions and reviewed target version. Typed exact-reference non-JS path
  must resolve one viable Case and bind confirmation to its reviewed version.
- Safe retries use a page operation id, deterministic per-member operation identity
  bound to actor, target, reason and reviewed input, and retained existing association
  identity. Expose existing persisted LastOperationKey through the receipt projection
  as needed; no new journal/schema. Recognize only the identical committed staff
  decision before acquiring a consumed lease; same destination alone is NOT replay.
  Changed input, another actor's association, stale receipt/Case or competing lease
  produces an honest conflict. Existing mutation hash/replay protection stays intact.
  For groups retain one submission-level choice, record/report per-member progress,
  never skip missing/elsewhere members as success, and retry only unchanged pending
  members after proving completed members belong to the same decision. Each successful
  member advances the Case version; next member uses that operation's returned or
  observed owned advancement, not an arbitrary refresh masking external edits.
  No new batch transaction framework is required.
- Reuse Cases/Create for extracted editable proposal when no viable Case exists.
  Offer acceptance/rejection clearly; reject/cancel returns to retained unallocated
  upload, without deletion or a fabricated permanent rejected Case state.
  Acceptance alone invokes existing allocation and Case/PO sequence. Preserve reason,
  version chain, address decision and typed failure/replay handling. For retained
  classified Audit only, use IStandaloneAuditEvidenceQueries and pass that receipt's
  existing evidence id to the existing acceptance gate; reject missing/foreign
  evidence or arbitrary switch to Audit. No fabricated evidence/new Audit policy.
- Recoverable form errors re-render entered selection/reason/operation/version and
  explain permission/version/lease/unavailable destination/partial completion.
  Unexpected errors remain visible through normal logging/error handling; no catch-all
  success or misleading no-reference claim after an uncertain commit.
  Existing ReconcileUnidentifiedDestinations remains the resolution/recovery owner;
  post-commit recovery failure must not pretend the association rolled back.
- Reuse existing Razor disclosure, controls and JS combobox. Distinguish request
  failure from no matches; invalidate stale requests immediately on input changes.
  Keep keyboard/ARIA, progressive enhancement and responsive layouts.
- Update direct affected callers, tests, FRDs and only affected generated snapshots.
  Accepted test fixtures explicitly accept; do not silently auto-confirm inside
  generic UploadAsync/ProcessQueuedAsync helpers or turn genuine manual tests into
  mailbox tests. Tests genuinely about mailbox automation use that actual channel.

## Expected files
| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify/Add | src/Pegasus.Core/Intake/DurableIntake.cs | Withhold manual unique-match automatic association; reuse link owner. |
| Modify/Add | src/Pegasus.Core/Intake/IntakeAllocation.cs | No automatic ManualUpload allocation or Case/PO. |
| Modify/Add | src/Pegasus.Core/Intake/IntakeContracts.cs | Bounded confirmation/replay read contract if needed from existing persisted data. |
| Modify/Add | src/Pegasus.Core/Intake/IntakeAssociationDestinations.cs | Core receipt/destination query contract and shared current viability policy. |
| Modify/Add | src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs | Manual images stay pending through automatic recovery until reasoned staff association. |
| Modify/Add | src/Pegasus.Infrastructure/Persistence/EfIntakeAssociationDestinations.cs | Receipt-scoped viable Case reads from real retained data. |
| Modify/Add | src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs | Share authoritative eligibility; preserve atomic version/lease/history/replay semantics. |
| Modify/Add | src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs | Map existing association identity for truthful same-decision retries if required. |
| Modify/Add | src/Pegasus.Infrastructure/DependencyInjection.cs | Register actual query port caller. |
| Modify/Add | src/Pegasus.Web/Presentation/UploadOutcome.cs | Truthful proposed/settled outcomes and viable choices. |
| Modify/Add | src/Pegasus.Web/Presentation/UploadCaseDecision.cs | Bound search/confirmation, no silent partial success, typed errors and safe retries. |
| Modify/Add | src/Pegasus.Web/Pages/UploadConfirmationPageModel.cs | Route/member, antiforgery, bound input and errors. |
| Modify/Add | src/Pegasus.Web/Pages/UploadStatus.cshtml.cs | Single-route context, versions, proposals, error re-render. |
| Modify/Add | src/Pegasus.Web/Pages/UploadStatus.cshtml | Current decision and errors. |
| Modify/Add | src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs | Group membership and one coherent decision over current members. |
| Modify/Add | src/Pegasus.Web/Pages/UploadGroupStatus.cshtml | Group viable options and error state. |
| Modify/Add | src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml | Suggested viable destinations, search, confirmation and reason. |
| Modify/Add | src/Pegasus.Web/Pages/Cases/Create.cshtml.cs | Reuse extracted-details acceptance and retained classified Audit evidence. |
| Modify/Add | src/Pegasus.Web/Pages/Cases/Create.cshtml | Explicit proposal acceptance/rejection, no Case/PO preview. |
| Modify/Add | src/Pegasus.Web/Program.cs | Affected composition only if constructor/registration changes. |
| Modify/Add | src/Pegasus.Web/wwwroot/js/site.js | Receipt-scoped suggestions, selected version, stale/error behavior. |
| Modify/Add | src/Pegasus.Web/wwwroot/css/site.css | Only necessary current-component option/error presentation. |
| Modify/Add | tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs | Manual proposal vs unchanged non-manual allocation. |
| Modify/Add | tests/Pegasus.Core.Tests/Intake/IntakeAssociationDestinationTests.cs | Shared viability rules. |
| Modify/Add | tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs | Manual pending first/replay/sweep and confirmed completion. |
| Modify/Add | tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs | Affected automatic channel expectations. |
| Modify/Add | tests/Pegasus.IntegrationTests/*.cs | Only direct affected upload/create/image/allocation tests and genuine shared seed consumers; no unrelated correction. |
| Modify/Add | tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs | Real browser confirmation, search, keyboard/error and responsive evidence. |
| Modify/Add | tests/Pegasus.IntegrationTests/Browser/QdosAllocationRecoveryBrowserTests.cs | Only affected proposal/accepted seed contract if required. |
| Modify/Add | tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs | Only affected pending decision/polling contract. |
| Modify/Add | docs/frd/frd-02-intake-and-source-identity.md | Settled manual confirmation and allocation exception. |
| Modify/Add | docs/frd/frd-12-operator-experience.md | Upload interaction agrees with FRD-02. |
| Modify/Add | docs/design/test-ui/pages/upload-status--*.html | Affected generated route snapshots. |
| Modify/Add | docs/design/test-ui/pages/upload-group-status--*.html | Affected generated route snapshots. |
| Modify/Add | docs/design/test-ui/pages/case-create--*.html | Affected generated proposal snapshots, verify catalogue name first. |

## Do not modify
- infra/**
- src/Pegasus.Infrastructure/Persistence/Migrations/**
- corpus/**
- docs/operations.md
- docs/principal-profiles/**
- .github/**
- .agents/skills/pegasus-release/**

## Constraints
No dependencies, schema, new allocator/link workflow, compatibility route, live cloud,
Outlook, Box or production data changes. No unrelated ticket scope or baseline repair.
Use Razor design and implementation skills, kanmer-docs for FRD reconciliation.
All test/build/capture work belongs to the named host verifier after explicit grant
in INTK-066/scratch/execution.md and idle transfer from prior host record.
Author does not start tests or host processes. Static reads/diffs may overlap.
Use one whole-ticket execution packet; no unconstrained step continuation.

## Ordered steps
1. Replace the three automatic ManualUpload destination paths and implement shared
   destination viability read/write ownership. Add focused Core evidence.
2. Wire bound single/group confirmation through existing mutations, reviewed versions,
   identical-decision replay, typed errors and honest partial outcomes. Add real SQL
   route tests for permissions, membership, stale state and replay.
3. Wire viable suggestions/search and editable proposal acceptance/rejection through
   existing Razor and Create owners, including retained classified Audit evidence.
   Reconcile the JS widget and direct production/test consumers.
4. Update the two governing FRDs and affected captures; obtain sequential frozen-input
   verification from the sole host verifier. Preserve failures and report deviations.
5. Review own diff for scope/correctness (not independent approval), complete checklist
   and post-implementation report, commit/push ticket branch and open PR to dev.
   Hand off to independent review; do not merge.

## Acceptance checks
- Single match still requires staff confirmation; zero viable matches offers editable
  proposal or honest unsupported-source handling; search lists only viable targets.
- New Case/PO absent before acceptance and after rejection; one allocation on acceptance
  and double-submit/retry. Non-manual automatic intake remains unchanged.
- Unauthorized, forged route/member, blank/long reason, stale versions, changed retry,
  competing lease, terminal/archive/report-sent and unsafe source fail closed.
- Single and group retries do not duplicate association/history or claim another action.
  Partial group failure identifies what committed and what remains, preserving input.
- Existing registered-image merge and Unidentified resolution remain operational.
- Browser proves keyboard selection/confirmation, request errors/stale response safety;
  render evidence covers affected routes and 1580/1100/760 responsive review as required.
- No hidden test helper restores obsolete automatic manual behavior; no dropped assertion.

## Commands
Run from recorded ticket worktree on PowerShell 7, by granted verifier only.
Read docs/engineering.md and docs/runbook.md for existing environment variables/setup.
1. dotnet build Pegasus.slnx
2. dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --no-build
3. dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~UploadOutcomeQueriesTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~GroupedIntakeWebTests|FullyQualifiedName~ImageIntake"
4. dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-build --filter "Category=Browser&FullyQualifiedName~UploadCaseSearchBrowserTests" -- xUnit.MaxParallelThreads=1
5. Remaining affected integration/architecture cohorts or exact-head qualifying CI
   selected from actual changed callers. Full solution scope is justified by the
   cross-channel automatic allocation behavior; serialize according to existing rails.
6. scripts/Update-TestUiSnapshots.ps1 scoped to upload-status,upload-group-status,
   case-create if changed, with actual owning focused captures. The script always
   includes TestUiFocusedRenderTests: account for that cohort, not unbounded all-Web.
   Frozen capture inputs then -SkipCapture update and -Verify; retain exact commands.
7. scripts/Test-MarkdownPlacement.ps1 against actual base/head plus relevant documentation
   checks, and git diff --check (static diff check may run without host slot).
If a named solution/command differs from tracked source, report and use only the
existing documented equivalent after primary correction; do not invent a new harness.

## Failure and deviation rules
Failing tests stop that verification phase and are retained; no weakened assertions.
Unknown files, missing current policy, dependency/schema needs, unsafe recovery,
contradictory retained Audit behavior or broader changes stop for primary disposition.
No live operations, no merge, no foreign cleanup. Keep claim/worktree on pause.

## Stop condition
Implementation and proportional evidence recorded truthfully, PR open to configured
integration branch, ready for independent kanmer-review. Do not merge or start another ticket.

## Primary affected-consumer correction — 8 September 2026

This addendum records newly discovered real callers rather than pretending they were in the initial map. Mail/Message consumes the old generic SearchAsync and must retain its trimmed minimum-two-character, bounded generic search through existing ISearchCases. Cases/Index has a real Attach form (line259 at baseline) and post-attach redirect handling; it is not an unused inheritance. Keep that Awaiting image capability wired through the new shared bound confirmation contract, including actual selected image origin/receipt versions, explicit reviewed target, safe replay and error rendering. The additional exact files are recorded in files/files.md's affected-consumer disposition. No new service/schema/dependency or unrelated queue redesign is authorized.

Because that existing queue includes registered Mail/Provider images, the shared query eligibility permits ImageIntakeRegistered across channels for explicit staff association; ordinary receipts remain ManualUpload scoped. Automatic Mail/Provider pairing remains unchanged. Update corresponding Core eligibility assertions with the exclusive test owner. Add CasesIndexWebTests and relevant MailWorkspaceWebTests to affected verification; update only affected cases-index captures with the actual catalogue scope, using the same sole-host ownership rules. Original source plan and earlier findings remain historical evidence.

## Resolved capture scope and group binding — 8 September 2026

Current files evidence: `files/files.md`@`dc89538d010c1b7f`. Catalogue scope for Cases/Index is `queues`, with generated `queues--*.html`; it replaces the provisional cases-index name above. Keep `/Search` snapshots out of scope. The four actual capture scopes are `upload-status,upload-group-status,case-create,queues` with existing owning cohorts and appended TestUiFocusedRenderTests, followed by retained capture verification and Test-UiCatalogue. No hand-written snapshot edits.

Static inspection identified a single root-class binding defect at both Index and direct grouped-member UploadStatus. Existing required server-loaded group binding includes rejecting manual multi-member group receipt POSTs and both search forms on single-item surfaces, redirecting grouped-member GET to the existing group workflow, and preserving lone/nonmanual retries. Reuse current authorized receipt read and group store/HasSiblingMembers; no new policy or orchestration owner. Existing grouped fixture must prove no single-member mutation through either surface. This is a clarification of already-required binding and necessary affected consumer correctness, not added feature scope.

## Operator-approved verification-gate removal — 8 September 2026

The operator explicitly instructed “remove the test” after reviewing the unsupported corpus-wide percentage gate, then approved the prerequisite execution-log split. Delete only `tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs`, including its arbitrary 60% field-presence assertions and test-only CSV helper. This is the explicit exception to the earlier no-unrelated-baseline-repair boundary. Retain all genuine exact-value extraction, routing, classification, upload confirmation and replay tests unchanged by this removal. Do not lower thresholds, exclude the test with filters, change extraction policy, alter corpus files or claim its previous failure passed. Preserve the failure in execution history; rebuild and run remaining non-browser regression before handoff. No new package or production behavior is added by this deletion.

## Operator revision — 9 September 2026

The operator explicitly orders removal of ALL Test UI snapshot/capture and Browser-category tests from local and CI and resubmission of this change. This supersedes every earlier capture/browser retention or verification requirement, the generated-only editing constraint, and the .github/docs/operations/Razor-skill exclusions only for this removal. Keep it in this requested PR; do not start an unrelated enhancement or replacement harness. Preserve all other implementation and evidence history.

Delete tests/Pegasus.IntegrationTests/Browser/**, tests/Pegasus.IntegrationTests/TestUi*.cs, scripts/Update-TestUiSnapshots.ps1, scripts/Test-UiCatalogue.ps1, scripts/Test-UiModes.ps1 and docs/design/test-ui/**. Remove capture startup hooks and capture-only helpers from HTTP test support and ReadinessEndpointTests; remove obsolete comments in affected tests. Remove Deque.AxeCore.Playwright and direct test Microsoft.Playwright references; regenerate affected NuGet lock data normally, preserving transitive Microsoft.Playwright needed by the actual report renderer and its runtime bootstrap/container packaging. Delete CI browser/test-ui jobs and catalogue step; remove obsolete Browser exclusion in SQL selection, retired path flags/regressions, UiMode switch and all callers. Update README.md, AGENTS.md unmanaged guidance, docs/index.md, docs/engineering.md, docs/runbook.md, docs/design/README.md, docs/frd/frd-12-operator-experience.md, docs/operations.md and .agents/skills/razor-pages-ui-implementation/SKILL.md for the removed system only. Check all tracked references; remove obsolete current instructions and repair links without rewriting unrelated domain snapshots or immutable supplied evidence. Archived Kanmer evidence remains truthful history, not active requirements.

Expected additional files: .github/workflows/ci.yml; scripts/Get-CiChangeFlags.ps1; scripts/Test-CiChangeFlags.ps1; scripts/Invoke-LocalDevelopment.ps1; scripts/Invoke-TestShard.ps1; scripts/Test-UiModes.ps1; scripts/Test-UiCatalogue.ps1; scripts/Update-TestUiSnapshots.ps1; tests/Pegasus.IntegrationTests/**; affected tests/**/packages.lock.json; README.md; AGENTS.md; docs/index.md; docs/engineering.md; docs/runbook.md; docs/design/**; docs/frd/frd-12-operator-experience.md; docs/operations.md; .agents/skills/razor-pages-ui-implementation/SKILL.md. Any further active caller/reference discovered is named before editing.

Acceptance: no browser tests or capture code/assets/jobs/modes remain locally or in CI; no replacement browser framework; nonbrowser unit/HTTP/SQL and report-renderer production behavior preserved. Validate Release build, focused affected HTTP cohorts, change-flag/sharding/local-script/parser/document checks and no dangling references, with frozen-source named final_verifier ownership. Existing long full-rail results retained honestly; do not run removed tests or claim visual approval. Stop with updated PR ready for independent review, no merge/deployment.

## Runtime installer affected callers

Additional exact allowed files: `scripts/Initialize-LocalDevelopment.ps1` and `scripts/Invoke-Doctor.ps1`. Point the Chromium installer at src/Pegasus.Infrastructure/bin because PDF generation is a production dependency, not a test dependency. Remove browser-test-only certificate commentary. Preserve actual runtime installation and checks.
