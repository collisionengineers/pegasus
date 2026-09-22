<proposed_plan>
# Remove Engineer-role casework restrictions

## Summary

Retain `Administrator`, `Engineer`, and `User` as account labels, but make them behaviorally identical for human casework. Any enabled staff account may perform engineering actions, hold an Engineer assignment, confirm findings, manage valuations and estimates, use its own Glass’s credential, generate/deliver reports, and qualify as a Sign-off Engineer when separately configured.

Administrator-only administration remains unchanged. Automation, Provider, and System Worker authority remains unchanged. No database migration is expected.

## Implementation changes

- Remove role inheritance as a casework mechanism. Make `ActionActor.IsInRole` test the stored role exactly, remove `StaffRoleCapabilities`, and retain explicit Administrator checks only for administration permissions.
- Replace Engineer checks in assessment, Engineer findings, Engineer’s Value adoption, repair specifications, estimate editing/import/acceptance, and Glass’s sessions with a human-staff requirement. Automation retains its existing draft/unconfirmed routes and cannot confirm professional outcomes.
- Remove Engineer checks from all Case Web guards. Valuation, market research, estimate/import, immutable report generation, fee-note generation, delivery preparation, and prepared-report sending must expose and accept the same actions for Administrator, Engineer, and User accounts, subject only to existing lifecycle, lease, version, evidence, credential, and readiness rules.
- Permit every enabled staff account to use `Assign to me` for Cases and Triage and to appear in Engineer assignment choices. Reduce assignment eligibility to account existence and enabled state; remove `HasEngineerRole` and role-filtered roster queries.
- Preserve “Engineer” as the workflow field and assignment label. Role no longer changes Work Centre behavior: all roles use the ordinary Office default unless the operator has selected another scope, and Mine continues to mean work assigned to the signed-in staff member.
- Make Sign-off Engineer eligibility independent of account role. Preserve the Administrator-managed flag, printed name, signature, enabled-state requirement, and single default. Changing an account to `User` must no longer clear valid sign-off settings.
- Treat Glass’s credentials and sessions as per-staff rather than per-Engineer. Allow an Administrator to configure them for any staff role; require the acting session owner to be enabled human staff; preserve per-user ownership, generation isolation, callback protection, and cross-account denial.
- Remove obsolete Engineer-only refusal labels, availability text, hidden-control conditions, role-filtered rendering, and comments. Preserve the existing Case layout and action placement; this is a permission change, not a redesign.
- Keep Administrator-only account, Principal, workflow, mailbox, automation-client, operational-report, and credential-management permissions intact.

## Contracts and documentation

- Update FRD-04 to state that the three role labels do not differentiate casework authority; only Administrator adds administration authority.
- Update FRD-13, FRD-16, FRD-24, FRD-25, and FRD-27 so assignment, self-assignment, professional confirmation, Engineer’s Value, estimates, reports, and AI-draft acceptance are staff actions rather than Engineer-role actions.
- Update FRD-11 and the runbook to describe Glass’s credentials as per-staff and report preparation/delivery as available to all staff.
- Update current architecture, design wording, capability links, and `CONTEXT.md` where they describe a named Engineer as the exclusive accepting actor. Preserve domain terms such as Engineer, Sign-off Engineer, Engineer’s Value, With Engineer, and Hand to Engineer.
- Add ADR-0054 to partially supersede ADR-0043’s per-Engineer eligibility decision while retaining its per-user encryption, ownership, generation, invalidation, and recovery design. Update the ADR index and ADR-0043 `superseded_by` metadata without rewriting its historical body.

## Test plan

- Rewrite Core authorization tests to prove all three staff roles can:
  - confirm every professional-finding field;
  - record and apply Engineer’s Value;
  - create, edit, duplicate, discard, import, and accept estimates;
  - record an Engineer finding;
  - self-assign a Case or Triage.
- Preserve negative tests proving Automation, Provider, and System Worker actors cannot gain human confirmation or Glass’s authority, while Automation can still write its existing unconfirmed fields and AI drafts.
- Update assignment persistence tests so every enabled staff role is selectable and missing/disabled accounts are refused. Cover role changes without losing an existing assignment or Sign-off Engineer configuration.
- Update Sign-off Engineer tests so any enabled flagged staff account with a signature is eligible, while unflagged, unsigned, disabled, or missing accounts remain ineligible.
- Add Web integration coverage using a `User` account for the previously restricted routes:
  - guide valuation save/fetch, market research, and Engineer’s Value;
  - finding-field save;
  - estimate save/import/acceptance and Glass’s control visibility;
  - report and fee-note generation, delivery preparation, and sending;
  - Case and Triage `Assign to me`.
- Update Glass’s gateway/callback tests to prove a `User` with its own configured credential can launch, resume, import, and close its session, while another staff account cannot consume it.
- Update Work Centre and Case-rendering tests to ensure controls are visible for all staff roles, Mine remains assignment-based, and no Engineer-only availability message remains.
- Keep existing lifecycle, edit-lease, concurrency, callback, source-evidence, readiness, and Administrator-route denial tests unchanged except where fixtures assumed Engineer role.
- Run focused Core and Integration suites during implementation, then the canonical locked verification:
  - `dotnet restore ./Pegasus.slnx --locked-mode`
  - `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
  - `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`
- Perform Case UI verification in read/edit and Scroll/Tabs modes at 1580×1000 and smaller desktop width, confirming that removing gates causes no reflow, duplicate controls, or stale refusal text.

## Assumptions

- “Any account type” means enabled human staff accounts only.
- The account role column and role selector remain because the labels may still describe staff, but `Engineer` has no casework privilege.
- Administrator remains the only role with administration permissions.
- Existing assignments, sign-off flags, credentials, and sessions remain valid; no persisted data conversion or compatibility path is required.
</proposed_plan>

# Step-by-step implementation guide

Status: planning only. Application changes and tests below have not been run.
The original session plan above is preserved verbatim. This guide expands it
and takes precedence where it explicitly corrects an instruction. It is a
temporary implementation handoff, not a new permanent requirements owner.

## 1. Start here

Pegasus has three human account roles: Administrator, Engineer and User.
An `ActionActor` identifies whoever is performing an operation. Its `Kind`
distinguishes human Staff from Automation, Provider and SystemWorker actors.
The business field called Engineer identifies the person assigned to a Case;
it is different from the account role called Engineer.

The requested result is that a User can do every casework action an Engineer
can do. The features stay; the account-role restrictions are removed.
Administrator still grants administration access. Sign-off remains a separate
Administrator-configured flag and signature, available on any staff role.

Use PowerShell 7. Every relative path below starts at this worktree root:

```powershell
Set-Location 'C:\Users\Alex\Documents\GitHub\pegasus-worktrees\remove-engineer-account-gates'
git branch --show-current
git status --short
git rev-parse HEAD
```

Expected branch: `task/remove-engineer-account-gates`. The starting commit is
`904903fd12f0b98be7d525aa26c60ea324b4cbd1`. The plan is initially the only
modified file. Preserve any additional work discovered later. Do not recreate
the worktree, reset it, switch to the original checkout, or discard changes.

Read these local instructions before implementing:

1. `AGENTS.md`, `docs/index.md` and `CONTEXT.md`.
2. `docs/engineering.md`, particularly Verification policy, and the setup and
   locked restore/build/test sections of `docs/runbook.md`.
3. `.agents/skills/pegasus-ui-guardrails/SKILL.md` and its
   `references/case-workspace.md` for the Web changes.
4. Applicable Razor implementation/review skills in `.agents/skills/` before
   modifying or reviewing routed Razor pages.
5. Available .NET test-writing and test-running skills before those activities.
   Use the repository's existing xUnit conventions and fixtures.

Repository orientation:

| Directory | What to change here |
| --- | --- |
| `src/Pegasus.Core` | Business authorization rules and request contracts |
| `src/Pegasus.Infrastructure` | Database queries, transaction checks and Glass's adapter |
| `src/Pegasus.Web` | HTTP handlers, rendered controls and account-settings script |
| `tests/Pegasus.Core.Tests` | Policy and command tests using small test doubles |
| `tests/Pegasus.IntegrationTests` | Database persistence and HTTP behavior tests |
| `docs/frd` | Current functional requirements |

An edit lease is the current staff member's permission to edit one record.
It carries a token and expiry; expected versions reject stale changes.
An operation key prevents the same submitted action being applied twice.
Removing a role check does not remove any of these conditions.

## 2. Lock the target behavior before editing

Use this matrix as the acceptance specification for the implementation and
tests. "Allowed" always means the existing prerequisites are satisfied.

| Action | Administrator | Engineer | User | Non-human actors |
| --- | --- | --- | --- | --- |
| Guide valuation fetch/save, research and calculator | Allowed | Allowed | Allowed | Existing scoped behavior only |
| Confirm findings and explicitly apply Engineer's Value | Allowed | Allowed | Allowed | Denied |
| Staff estimate save/edit/import/duplicate/discard/accept | Allowed | Allowed | Allowed | Existing draft/import routes only; never acceptance |
| Generate report/fee note, prepare and send report | Allowed | Allowed | Allowed | Existing restrictions retained |
| Be assigned as Engineer or use Assign to me | Allowed | Allowed | Allowed | Cannot impersonate staff for self-assignment |
| Be configured as Sign-off Engineer | Eligible | Eligible | Eligible | Ineligible |
| Use own configured Glass's credential/session | Allowed | Allowed | Allowed | Denied |
| Administer staff, credentials, Principals and settings | Allowed | Denied | Denied | Existing restrictions retained |

Keep readiness, source provenance, draft acceptance, signatures, enabled
accounts, ownership, archive/lifecycle restrictions, antiforgery and audit
history. A report signatory need not be the person clicking Generate. The
selected signatory must still have an eligible profile.

Do not rename business fields, lifecycle states, report text such as Engineer's
Value, historical event kinds or persisted role values. Do not turn User
accounts into Engineer accounts. This must work with an actual User account.

## 3. Establish the complete edit inventory

Run these searches before editing and again at completion. Search by symbols
rather than line numbers, because line numbers change as edits accumulate.

```powershell
rg -n 'StaffRoleCapabilities|HasEngineerRole|RequireEngineer|RequireSelfAssigningEngineer|ActorIsEngineer|EngineerOnly|EngineerRoleRequired' src tests
rg -n 'StaffRole\.Engineer|StaffRoleNames\.Engineer|IsInRole|RequireRole' src --glob '*.cs' --glob '!**/Migrations/**'
rg -n 'Engineer|signatoryAllowed|signOff|account-role' src/Pegasus.Web/wwwroot/js/accounts.js src/Pegasus.Web/Pages/Administration/Accounts
rg -n -i 'only.*engineer|engineer.*only|per-engineer|engineer capabilities|engineer act' docs CONTEXT.md
```

Keep all-three-role `[Authorize]` attributes: they already allow User.
Keep role parsing, stored role labels, account administration and historical
migration definitions. Classify matches by behavior; never globally replace
the word Engineer. Include direct HTTP posts, fragment requests, callbacks,
Core commands and persistence writers in the review.

## 4. Change Core authorization and update every caller

### 4.1 Exact role identity

File: `src/Pegasus.Core/Identity/IdentityContracts.cs`.

1. Keep `StaffRole`, `StaffRoleNames`, and the exactly-one-recognized-role
   invariant in `ActionActor.Staff`.
2. Change `ActionActor.IsInRole` to `Roles.Contains(role)`.
3. Delete `StaffRoleCapabilities` after replacing its callers below.
4. Remove the obsolete comment claiming Administrator stores one role but
   implicitly belongs to every role. All staff can perform casework because
   they are Staff; management still checks Administrator explicitly.

File: `src/Pegasus.Core/Identity/StaffAuthorization.cs`.

Keep the existing access-right matrix. In particular, `PerformCasework`
also allows Automation, so it alone is insufficient for human acceptance.
Reuse `AccessStaffApplication` when a common explicit human-staff guard is
needed; it already requires `ActorKind.Staff`. Do not add a permission registry,
new service or role alias to make Users look like Engineers.

### 4.2 Findings and valuation

| File | Exact edit |
| --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | In `RequireFindingConfirmationAuthority`, remove the Engineer-role predicate, retain the Staff-kind check and change the refusal to say authenticated staff. Update the XML comment and readiness instructions that exclusively name an Engineer reviewer. |
| `src/Pegasus.Core/Cases/CaseContracts.cs` | In Engineer-finding request validation, retain Staff kind, nonempty parsed staff ID and assessment validation; remove only the Engineer-role condition and revise its refusal wording. |
| `src/Pegasus.Core/Cases/CaseWorkspace.cs` | Keep the finding-authority call for submitted finding fields. It now permits all staff. Replace its estimate authority call as described in 4.3. |
| `src/Pegasus.Core/Assessment/Valuations.cs` | Keep `RequireActor` and its finding check for `EngineersValue`; update comments to describe staff confirmation. |
| `src/Pegasus.Core/Assessment/ValuationCalculations.cs` | Keep `ValidateApply` calling the revised finding-authority rule. Preserve calculation, basis, reason and explicit Apply requirements. |
| `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` | Keep its finding-authority call, including when an edit changes a former Engineer's Value to another source. It must still reject non-human confirmation/clearing. |

Do not delete `IsFinding` from `AssessmentContracts.cs`: it describes
professional data and its confirmation semantics. All staff may now confirm
these fields, while Automation values remain unconfirmed. Do not expose a
generic write to the adopted Engineer's Value field as part of this change.

### 4.3 Estimates and Glass's

File: `src/Pegasus.Core/Assessment/RepairSpecifications.cs`.

Rename `RequireEngineer(ActionActor actor)` to
`RequireStaffAuthor(ActionActor actor)`. Retain its null check, Staff-kind
check and `InvalidOperationException` shape; remove the role predicate.
Use a refusal that names authenticated staff. Keep acceptance/provenance checks.

Update all calls to the renamed method in these files:

- `src/Pegasus.Core/Assessment/RepairSpecifications.cs`
- `src/Pegasus.Core/Assessment/Estimates.cs`
- `src/Pegasus.Core/Assessment/GlassRepairEstimates.cs`
- `src/Pegasus.Core/Cases/CaseWorkspace.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`
- `src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs`

In `Estimates.cs`, keep the existing Automation branches: a draft save must
still cite its allowed AI job/source, and import must still produce a Draft.
`ValidateSetCurrent`, Duplicate, Discard and human line-amendment paths must
require Staff, now without a role test. Do not accidentally replace these with
`PerformCasework`, which would admit Automation acceptance.

For Glass's, preserve Launch, Resume, estimator-URL and callback ownership,
correlation, credential-generation and account-slot checks. Keep close-session
validation and explicit external-session-closed confirmation. Review
`EfGlassRepairEstimateSessionStore.cs`, `GlassRepairEstimateCaseAuthority.cs`
and `EfPerUserExternalCredentialStore.cs` to ensure the revised policy reaches
their writers and disabled accounts still fail through existing checks.

## 5. Permit all staff assignments and sign-off profiles

### 5.1 Case/Triage assignment

1. In `src/Pegasus.Core/Identity/CaseEngineerEligibility.cs`, change the record
   to `CaseEngineerEligibility(bool AccountExists, bool IsEnabled)`.
   Keep the existing interface and business name.
2. In `src/Pegasus.Infrastructure/Persistence/EfCaseEngineerEligibility.cs`,
   delete `EngineerEligibleRoleNames` and the role-membership subquery.
   Project only account existence and enabled state. Missing account returns
   `new CaseEngineerEligibility(false, false)`.
3. In `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs`, remove only the
   `HasEngineerRole` rejection in `CaseEngineerEligibilityPolicy`.
   Keep checks used by assignment, StartCaseWork and ReturnCaseToEngineer.
4. Rename `RequireSelfAssigningEngineer` to `RequireSelfAssigningStaff`.
   Retain Staff kind and parsed nonempty staff ID; remove the role predicate.
   Update callers in `Lifecycle/AssignCaseToMe.cs` and
   `Triage/TriageLifecycle.cs` and revise the refusal wording.
5. In `src/Pegasus.Core/Operations/OperationsSnapshot.cs`, remove the role
   predicate from `NeedsAttentionPolicy.CanTake`; keep Staff kind and allowed
   item kinds. Preserve owner, state and unassigned checks at callers.
6. In `src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs`, update
   `ICaseEngineerChoices.GetAsync` to return enabled staff with a displayable
   username in the existing stable order. Remove its Engineer/Admin filter.
7. Replace the role filters in `src/Pegasus.Web/Pages/Index.cshtml.cs` and
   `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` with enabled-account filters.
   Preserve pagination and naming behavior; avoid unrelated query redesign.
8. Remove role tests from `CanAssignToMe` in the Work Centre and
   `src/Pegasus.Web/Pages/Cases/Details.Frame.cs`. Keep Case-state conditions.
   Triage visibility follows the updated `CanTake` policy.
9. Change Work Centre `DefaultScope` to Office for all roles. Preserve explicit
   Office/Mine selections and owner-based Mine filtering.

Update all constructor usages and test doubles after removing the third
eligibility field. Search `CaseEngineerEligibility` and `HasEngineerRole` to
find them. EVA handoff/export callers also consume this eligibility policy;
test them with a User assignee instead of leaving an implicit role restriction.

### 5.2 Sign-off eligibility

| File | Exact edit |
| --- | --- |
| `src/Pegasus.Core/Identity/StaffAccountAdministration.cs` | Remove the role parameter from `SignOffEngineerEligibility.IsEligible`; require enabled, flagged and nonempty signature. In settings normalization, set `isSignOffEngineer = request.IsSignOffEngineer`, retaining the default-requires-flag relationship. Do not clear sign-off data when role becomes User. Delete the unused `SignOffEngineerRequiresEngineerRole` error member. |
| `src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs` | Remove the Engineer/Admin role filter from `ListSignOffEngineersAsync`. Update both list and single-profile calls to the new signature. Preserve account validity checks and printed-name/signature mapping. |
| `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs` | Update `ApplySignOffSettings` to the new eligibility signature. Preserve signature validation, expected versions, default uniqueness, transaction history and security-stamp behavior on role changes. |
| `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml` | Remove `signatoryAllowed`, the role-based disabled attributes and the hidden false input inserted for a User. All account roles expose the existing sign-off controls. Keep self-role-change protections. |
| `src/Pegasus.Web/wwwroot/js/accounts.js` | Remove `syncSignOffEligibility`, its calls and its role-change listener. Remove now-unused role/sign-off/default variables. Preserve form reset, mount binding and unsaved-settings protection on Manage login navigation. |
| `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml.cs` | Remove the deleted error member's message mapping. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Remove the User-role early return from `SignOffState`; remove the now-unused Engineer-role-required message. |

Search `SignOffEngineerEligibility.IsEligible` to update every remaining
caller and test. Existing User accounts start with their current settings;
do not manufacture signatures or retroactively restore cleared settings.

## 6. Remove Web gates on every entry point

Primary file: `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`.

1. Delete `ActorIsEngineer` and its assignments. Remove it from estimate
   editable/duplicate/current predicates and Glass's launch/close predicates.
   Keep assessment access, lifecycle, estimate state, credentials and ownership.
2. Make `CanEditAssessmentField` depend on existing engineering edit authority,
   with no role or `IsFinding` permission branch. Keep field validation in Core.
3. Remove the Engineer-only branch from `ImportCondition`.
4. In the Glass's model loader, replace the Engineer check with the existing
   authenticated Staff identity requirement and staff-ID parsing. User accounts
   must load their own credential status and own session.
5. In `GuardSectionCommandAsync`, remove the `engineerOnlyRefusal` parameter
   and role rejection. Update `GuardReportCommandAsync` and
   `GuardValuationCommandAsync` calls. Keep actor, assessment access,
   read-only-state, operation-key and lease-token checks.
6. In `GuardEstimateEditAsync`, remove the role rejection; retain its other
   checks and current Case-version read.
7. In `OnPostCloseGlassAsync`, retain authenticated actor/ID, session-owner and
   access checks; remove the Engineer-role clause.
8. In `Pages/Cases/Shared/_CaseEstimate.cshtml`, remove `Model.ActorIsEngineer`
   from `offersNewEstimate`. Review action conditions throughout that partial.
9. Delete `EngineerOnlyImport` in `Presentation/OperatorLabels.cs` after all
   references have been removed. Update nearby descriptions that claim only
   Engineers can operate controls.
10. In `Pages/Integrations/Glass/Callback.cshtml.cs`, simplify the helper that
    identifies staff to `actor.Kind == ActorKind.Staff`. Preserve its anonymous
    bounce, authentication, correlation and owner checks.

Entry-point checklist (handler names are exact, URLs come from each page's
`@page` declaration and generated forms):

| Page model | Handlers to trace and verify |
| --- | --- |
| `Pages/Cases/Details.Valuation.cs` | `PreviewValuation`, `ApplyValuation`, `StartMarketResearch`, `SaveValuation`, `GetValuation` |
| `Pages/Cases/Details.cshtml.cs` | `Save`, `SaveEstimate`, `EditLine`, `DuplicateEstimate`, `DiscardEstimate`, `SetCurrentEstimate`, `ImportEstimate`, `CompleteEstimateImport` |
| Same Case model | `LaunchGlass`, `ResumeGlass`, `CloseGlass` |
| Same Case model | `GenerateReportDraft`, GET/POST `PreviewReportDraft`, `EstimateDocument`, `GenerateReport`, `GenerateFeeNote`, `PrepareReportDelivery`, `SendPreparedReport`, `GeneratedArtifact` |
| `Pages/Cases/Workflow.cshtml.cs` | `AssignEngineer`, `AssignToMe`, `SetSignOffEngineer`, `RecordEngineerFinding` |
| `Pages/Index.cshtml.cs` | `AssignEngineer`, `AssignToMe`, `AssignTriageToMe`; assignment dialog and default scope |
| `Pages/Triage/Details.cshtml.cs` | `AssignToMe`, ordinary assignment through `Action`, roster/visibility loading |
| `Pages/Administration/Accounts/Index.cshtml.cs` | Account `Settings`, including role changes and sign-off settings |
| `Pages/Administration/Glass/Index.cshtml.cs` | Administrator configures any target staff role; target user cannot administer credentials |
| `Pages/Integrations/Glass/Callback.cshtml.cs` | Return/bounce and completion using the session owner's User account |

Do not add an edit-lease requirement to SendPreparedReport or previews merely
to match generation: preserve each operation's existing contract. The old
Assessment route is a redirect; ensure it still lands on usable controls for
a User. Full-page, direct-section and lazy-section rendering must agree.

## 7. Update tests alongside each implementation step

Use `[Theory]` and `[InlineData(StaffRole.Administrator)]`, Engineer and User
where the same behavior must work for all roles. Build each actor with a
nonempty GUID and exactly one role. Test successful state changes/history,
not merely the absence of an exception or an HTTP redirect.

Replace obsolete User-denial tests with role-parity tests. Keep or add separate
Automation, Provider and SystemWorker denial tests for human-only actions.
Do not remove negative tests for stale versions, missing leases or other
conditions just because those tests use a User fixture.

### Core test edits

All paths below are relative to `tests/Pegasus.Core.Tests/`.

| Test file | Required changes and assertions |
| --- | --- |
| `Identity/IdentityUseCaseTests.cs` | Replace Administrator role-inheritance assertions with exact stored-role assertions. All three roles have ordinary casework; only Administrator has management rights. Parameterize sign-off eligibility across roles at callers, with disabled/unflagged/unsigned rejection. |
| `Identity/StaffActorFactoryTests.cs` | Rename the scalar-Administrator inheritance test; assert one actual role and no inferred Engineer role. Keep missing, unknown and multiple-role rejection. |
| `Assessment/AssessmentPolicyTests.cs` | Replace `NonEngineerStaffCannotRecordFindingFields` with successful Staff-role theories. Exercise every finding definition, with the adopted Engineer's Value still refused on generic save. Keep ordinary-field and Automation-unconfirmed tests. |
| `Cases/EngineerFindingPolicyTests.cs` (new) | Directly test the currently untested `EngineerFindingPolicy`: all three staff roles may record a finding when the Case is assigned to them and in an eligible state; non-staff, unassigned/wrong assignee and wrong-state cases remain refused. |
| `Assessment/ValuationTests.cs` | Replace Engineer-exclusive actor expectations with all-staff success, preserving typed source validation and human-only save rules. |
| `Assessment/ValuationCalculationTests.cs` | Replace `AdministratorMayAdoptAnEngineersValueWhileUserAndNonStaffCannot` with all-staff Apply success plus separate non-staff rejection. Assert calculated/adopted value and actor attribution. |
| `Assessment/RepairSpecificationPolicyTests.cs`, `Assessment/EstimateTests.cs`, `Assessment/EstimateLineAmendmentTests.cs` | Replace `AStaffUserWhoIsNotAnEngineerCannotSaveAnEstimate`; cover save, amendments, import, duplicate, discard and acceptance for all roles. Preserve AI job/source restrictions. |
| `Assessment/GlassRepairEstimateSessionPolicyTests.cs` | User owner may close; another staff ID and all non-human kinds remain refused. |
| `Cases/CaseWorkspaceTests.cs`, `Cases/CaseRecordGapsTests.cs` | User saves finding/estimate sections and records findings through the named command; preserved values and attribution agree with Engineer behavior. |
| `Lifecycle/AssignToMeTests.cs` | Replace `AUserCannotTakeACase` and Triage User refusal. For each staff role assert assigned ID equals actor ID; already assigned and wrong-state records still refuse. |
| `Lifecycle/AssignCaseEngineerTests.cs` | Update eligibility doubles to two booleans; remove the non-Engineer failure case and retain missing/disabled failures and sign-off default selection. |
| `Operations/OperationsUseCaseTests.cs`, `Operations/DashboardBoundaryTests.cs` | Check Take/Mine eligibility for all staff roles and retain non-staff boundaries. |

### Integration and Web test edits

All paths below are relative to `tests/Pegasus.IntegrationTests/`.

| Test files | Evidence required |
| --- | --- |
| `CaseValuationWebTests.cs`, `CaseValuationV26WebTests.cs`, `AssessmentPersistenceIntegrationTests.cs` | A real User request fetches/saves a guide, starts research, previews calculation and applies Engineer's Value. Verify persisted result and attribution; disconnected providers still give the existing notice. |
| `CaseEditModeWebTests.cs`, `CaseWorkspacePersistenceTests.cs`, `CaseEngineerSectionsWebTests.cs` | User gets live controls under its own lease, saves finding fields, and sees committed results in full and fragment responses. Colleague lease and Held/Completed restrictions remain. |
| `AssessmentEstimateImportWebTests.cs`, `CaseEstimateHeaderWebTests.cs`, `AssessmentPersistenceIntegrationTests.cs` | User imports a Draft, completes retained import and accepts it; save/amend/duplicate/discard work. Verify import alone never changes Current. |
| `CaseCapabilityPagesTestSupport.cs` | Add a role parameter to the shared test identity/edit-mode helper so real User HTTP requests can reuse the current anti-forgery and lease setup instead of hard-coding Engineer. |
| `Reports/AssessmentReportDraftWebTests.cs` | Exercise report-draft generation and both preview entry points with a User account; preserve readiness and immutable-artifact rules. |
| `CaseEngineerChoicesPersistenceTests.cs` | Rename `EngineerChoicesReturnOnlyEnabledEngineersInStableOrder`; include enabled accounts of all roles and exclude disabled accounts in stable order. |
| `CaseWorkflowPersistenceTests.cs`, `CaseWorkflowWebTests.cs`, `WorkCentreWebTests.cs`, `TriageQueuesWebTests.cs` | User assignment and all self-assignment entry points work. Update two-boolean eligibility fixtures. Replace `MissingDisabledOrNonEngineerStaffCannotBeAssigned` with missing/disabled cases and separate User success. Default scope is Office for every role. |
| `StaffAccountAdministrationPersistenceTests.cs`, `StaffAccountsAndRolesWebTests.cs` | Administrator flags User with valid printed name/signature and selects it as default. Engineer-to-User settings save preserves supplied sign-off data; selecting No explicitly clears it. Non-admin management remains forbidden. |
| `GlassCredentialAdministrationWebTests.cs`, `ExternalCredentialIsolationTests.cs` | Administrator configures a User's credential; the User can use its own material through allowed operations, cannot reveal stored passwords or access another account's material. |
| `GlassRepairEstimateGatewayTests.cs`, `GlassRepairEstimateCallbackWebTests.cs`, `GlassRepairEstimatePersistenceTests.cs` | User-owner launch, resume, callback import and close succeed with test provider adapters. Rewrite the non-Engineer visibility denial; retain no-credential, other-owner, stale-generation and disabled-account refusals. |
| `Reports/CaseReportGenerationPersistenceTests.cs`, `Reports/CaseReportDeliveryWebTests.cs`, `Reports/CaseReportDeliveryPreparationPersistenceTests.cs`, `CaseReportApprovalWebTests.cs` | User generates report and fee note, prepares delivery and explicitly sends it using existing test transports; correct signatory and immutable generation are retained. Stale preparation and missing Sent evidence still fail as before. |
| `CustodyOutboxIntegrationTests.cs`, `EvaCaseEvidenceReaderTests.cs` | An eligible User assignee/signatory passes the relevant native/EVA projection checks; missing signature/disabled selection still fails. |
| `AutomationAssessmentIngressTests.cs`, `AutomationMcpIngressTests.cs`, `ProviderApiSubmissionTests.cs` | Existing transports cannot obtain newly broadened human authority; AI output stays draft/unconfirmed and provider access remains scoped. |

Use existing local test hosts, fake provider gateways and disposable database
fixtures. For HTTP tests obtain the antiforgery token and lease exactly as
existing positive tests do; changing a role claim alone is insufficient if a
test exercises a real persisted Identity account. Use a fresh real User fixture
where current-account checks apply. Exercise direct POSTs, not only visibility.

Review `StaffSignInSecurityTests.cs` and existing cookie tests for disabled,
deleted and revoked-session behavior. Do not loosen these to make parity pass.
No automated test harness for `accounts.js` currently exists. Do not add a
JavaScript framework for this permission change. Verify changing the role to
User leaves signature/default fields enabled and intact in the browser
walkthrough in step 9; server-side persistence is proved in the account
administration integration tests. Also confirm Cancel still resets the form.

## 8. Make current documentation tell the same story

Edit these owners in the same implementation change. Preserve settled domain
names and historical evidence; change exclusive account-role claims.

| Document | Required edit |
| --- | --- |
| `docs/frd/frd-04-parties-accounts-and-access.md` | Explain exact roles, equal human casework rights and Administrator-only management. State every enabled staff role is assignable and can be configured for sign-off. Describe vendor credentials as per-staff. |
| `docs/frd/frd-03-triage.md` | Explain all enabled staff may be assigned and self-assign when the existing state/lease rules allow. |
| `docs/frd/frd-13-case-lifecycle-and-workflow.md` | Replace role-exclusive self-assignment and Engineer's Value wording. Preserve workflow labels and signatory defaults. |
| `docs/frd/frd-15-work-centre-queues-and-search.md` | Document Office default for all roles and role-independent Take/Mine behavior. |
| `docs/frd/frd-16-case-record-workspace.md` | All staff may use valuation, findings, estimates, reports and assignment controls under normal edit authority. |
| `docs/frd/frd-24-engineer-findings-damage-valuation-and-settlement.md` | Explicit Apply/confirmation is a human staff act; numerical, provenance and readiness contracts are unchanged. |
| `docs/frd/frd-25-repair-estimates-imports-and-glasss-sessions.md` | Any human staff author may change/accept estimates and use its own Glass's session. Imports remain Draft until explicitly accepted. |
| `docs/frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md` | Human staff, regardless of role, confirm findings and accept AI estimates. Automation still cannot accept or send autonomously. |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Clarify any Engineer-only reviewer language while retaining the exact non-human action inventory. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Report generation/preparation/send are human staff actions; the separate selected signatory profile remains required. |
| `docs/frd/frd-17-administration-workspace.md` | Sign-off controls apply to any staff role; administration remains Administrator-only. |
| `CONTEXT.md` | AI Proposal acceptance names an authorized human staff member instead of requiring Engineer role. Keep Engineer's Value and lifecycle vocabulary. |
| `docs/design/README.md`, `docs/runbook.md` | Replace functional role-exclusive control/credential claims; retain geometry, provider setup and protection guidance. |
| `docs/engineering/configuration.md`, `docs/prd/pegasus-product.md` | Describe the existing per-user vendor credential/session protection and any casework availability in staff-role-neutral terms. |
| `docs/current-architecture.md`, `docs/capabilities.md` | Correct affected descriptions to the revised policy and existing owners; keep capability IDs and valid links. |

Correction to the original plan: do not create ADR-0054 for this work.
`docs/adr/0043-per-engineer-vendor-credential-protection.md` already decides
per-user protection bound to user/provider/generation. That technical design
does not change. Account eligibility is functional behavior owned by FRD-04
and FRD-25. Leave issued ADR bodies and IDs intact; do not claim they were
rewritten or superseded by a new architecture decision. Historical mentions
of Engineer in ADRs, old mockups and deployment records are not current gates.

Do not update `docs/operations.md` to claim a deployment. Do not rewrite
historical migrations, source evidence or `corpus/`. Review remaining current
documentation search hits and explain intentional retained domain terms in
the implementation handoff.

## 9. Verify in a controlled order

The implementing primary agent is the sole heavy verifier for this task.
Use one platform, Windows with PowerShell 7, and no competing build/test jobs
on this host. Follow the runbook for the local SDK and disposable database
prerequisites; do not point tests at production services.

After the first coherent source/test edits, run:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

Run the focused owning classes first. Example valuation tranche:

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ValuationTests|FullyQualifiedName~ValuationCalculationTests|FullyQualifiedName~AssessmentPolicyTests'
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'Category!=Corpus&(FullyQualifiedName~CaseValuationWebTests|FullyQualifiedName~CaseValuationV26WebTests)'
```

Expand the class filter for each completed tranche using the tables in step 7.
After any source/test edit, rebuild before using `--no-build`. Inspect failures
and fix their cause; keep failure evidence. Do not replace an assertion with a
weaker one merely because an old test fails.

After all changes and focused tests pass, run the full regression once:

```powershell
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter 'Category!=Corpus'
pwsh ./scripts/Test-DocumentationLinks.ps1
git diff --check
git status --short
```

Record the checked source revision and dirty diff, exact commands, pass/fail/
skip totals, failures and outstanding checks. A skipped test is not a passed
scenario. .NET verification is for implementation; editing only this plan
does not require restore, build or tests.

### Browser acceptance walkthrough

Use an isolated local test instance with existing approved test adapters.
Prepare enabled Administrator, Engineer and User accounts, a disabled account,
a writable Case, a Held/Completed Case and a second staff-owned edit lease.
Use supplied valid estimate/report fixtures and test provider/mail transports.

1. Sign in as User. Confirm Work Centre opens Office; switch to Mine and use
   Assign to me on a suitable Case and Triage. Confirm ownership and history.
2. Enter Case edit mode. Save guide figures, request research and explicitly
   apply a calculated Engineer's Value. Refresh and confirm saved values.
3. Save settlement/finding fields, including accepting an awaiting proposal.
   Confirm the value and resolving staff identity in persisted history.
4. Import a supported estimate, confirm it is Draft, then Use estimate.
   Exercise editing, duplicate and discard on suitable draft versions.
5. With a test Glass's credential, launch/resume/return/close through the
   mocked provider journey. Another staff account must not own that session.
6. Generate a report and fee note, prepare recipients and send through the
   test transport. Verify the selected signatory and recorded send actor.
7. As Administrator, configure User sign-off with a valid signature/default.
   Change another flagged account from Engineer to User and verify its profile
   remains valid. Confirm User still cannot open Administration.
8. Recheck Held/Completed, colleague lease, missing signature, no credential
   and revoked/disabled-account states. Each retains its supported refusal.
9. Compare affected read/edit views at 1580x1000 and 1280x900, including Case
   Scroll and Tabs modes. Controls stay in their existing positions without
   clipped content or obsolete Engineer-only explanations.

A live Glass's, Outlook or Box operation is not part of this local walkthrough.
Report external integration proof separately if it requires a later authorized
test; a fake-provider result proves application behavior, not live service use.

## 10. Completion checklist and handoff

- [ ] Every staff role passes each formerly Engineer-only operation with the
  same prerequisites, including actual persisted User-account HTTP tests.
- [ ] Role predicates no longer determine findings, valuation, estimates,
  Glass's use, assignment, self-assignment or signatory eligibility.
- [ ] No `StaffRoleCapabilities`, `HasEngineerRole`, `RequireEngineer`,
  `RequireSelfAssigningEngineer`, `ActorIsEngineer` or Engineer-only error
  references remain in current implementation/tests after their replacements.
- [ ] Human-only checks remain for confirmation/acceptance and external
  session ownership. Administrator and non-human boundaries pass regression.
- [ ] Every changed signature has all production and test callers updated;
  there are no compatibility wrappers or unused old branches.
- [ ] Account role values and existing data remain intact; no schema change,
  data backfill, secret reset or feature flag was added.
- [ ] Relevant FRDs, glossary and current implementation descriptions agree.
- [ ] Focused tests, full regression, documentation links and browser checks
  are recorded with limitations rather than assumed successful.
- [ ] Final diff contains only this task and its required tests/documentation.

The final implementation handoff should state changed behavior, affected
surfaces, verification results and any remaining evidence gaps. Keep the
implementation local until a separate instruction requests publication,
merging or release. Saving or extending this plan does not execute its steps.
