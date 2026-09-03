# Plan — ENG-029 (2026-09-02, gpt-5.6-terra high)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research`
at `897db953` (= `origin/dev`). The first run failed before starting
(exit 126: the 39 KB brief exceeded the command-line argument limit through
the npm shim); the retry passed the brief as a file path and exited 0.
`git status --porcelain` was empty after both runs; no reset was needed.
The following claims were re-run by the wrapper in the same checkout and
all confirmed:

- `AssessmentContracts.cs:105` already carries the four outcome codes
  `total_loss`, `repairable`, `cash_in_lieu`, `contract_repair`; the scalar
  paths `assessment.outcome/category/salvage_value`,
  `narrative.engineers_comments/history_check`, `fee.agreed_fee/
  description_lines` and legacy `costs.recovery_charge/storage_charge/
  repairer_vat_registered` are at lines 53–67.
- `AssessmentPolicy.cs:142–153` and `284–300` make salvage category and
  value a Core requirement only when the outcome is `total_loss`.
- `SaveAssessmentRequest` (`AssessmentContracts.cs:270`) takes CaseId,
  ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken and the
  field map; `ISaveAssessment` is registered at
  `Infrastructure/DependencyInjection.cs:350`.
- `CaseMutationPageModel.cs` carries `LeaseToken` (110), `NewOperationKey`
  (112), `ClearLeaseState` (140+) and the command runner at 241–273 that
  clears or re-stores the lease authority per result.
- `EstimateTotals.Compute` is `Estimates.cs:92`;
  `AssessmentReportProjection.Prepare` is `AssessmentReportProjection.cs:100`
  and returns named `AssessmentReadinessItem` reasons (no percentage).
- `AssessmentWorkspace.cs:40/56` exposes `IsReadOnly` via
  `AssessmentAccessPolicy.IsReadOnly`.
- `Reports/AssessmentReportDraftWebTests.cs:35–55` is the
  `IntakeWebApplicationFactory` + `FakeGetCase` + antiforgery `Form(...)`
  pattern the new test reuses; `RecordingStores` is a private nested class
  at `AssessmentEstimateImportWebTests.cs:654`, so it is a pattern to copy,
  not a type to reference.
- `docs/design/test-ui/catalogue.json:324–344` lists three existing
  `Pages/Cases/Details.cshtml` states (`default`, `unavailable`,
  `conflict`).

One wrapper correction, folded into "Commands" below: Codex handed any
snapshot regeneration to UIIMP-014. The repository rule (CLAUDE.md,
Commands) and the CASE-038/ENG-034 file-map corrections put a snapshot that
this PR's own diff changes into this PR, as a capacity-one lease, so CI's
verify stays green; only *new* Case-record states and their catalogue
entries remain UIIMP-014's. Nothing else was changed; the checklist was
expanded to one item per verifiable step.

Board dependencies at planning time: ENG-034, ENG-035, CASE-038, CASE-040,
PLAT-068 and DOCS-017 are all in `preparing`. Step 2 also needs the
explicit whole-file hand-off of `Details.cshtml.cs` recorded on this ticket
before implementation starts.

## Research basis

Re-verified read-only at `897db9530a45063e8f684f2800685afbfdced006`:

- `git status --short; git rev-parse HEAD` confirmed the clean detached
  checkout and revision.
- `rg` over `AssessmentContracts.cs` confirmed the four outcome codes,
  existing scalar paths, and legacy cost paths.
- `rg` over `EfCaseAssessmentStore.cs` and
  `AssessmentReportProjection.cs` confirmed lease clearing at save,
  `AssessmentReportProjection.Prepare`, and `EstimateTotals.Compute`.
- `Test-Path` confirmed both new partials and the new test file are absent.
- `rg` confirmed the reusable web-test support:
  `AssessmentWorkspaceTestData.Create`, `FakeGetAssessmentAccess`,
  `IntakeWebApplicationFactory`, and the local `RecordingStores` pattern.

The merged contracts, path ownership, shared-lock sequencing, sign-off
projection, D41 vocabulary/projection, route move, and snapshot ownership are
assumed from the board documents.

## Preconditions and scope

Implementation waits for [[ENG-035]] to merge its vocabulary, migration, Core
derived figures, and report projection; [[ENG-034]] to merge the Case-section
shells and moved report entry points; [[PLAT-068]], [[CASE-040]], and
[[DOCS-017]] to merge the sign-off account, Case field/default, and renderer
tuple; and an explicit whole-file hand-off of
`src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` from [[CASE-038]] and
[[CASE-040]].

ENG-029 changes only:

| Path | Action |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` | Change ENG-034 shell body |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` | Change ENG-034 shell body |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Change after explicit hand-off |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Change under serialized shared lock |
| `tests/Pegasus.IntegrationTests/CaseAssessmentEditorsWebTests.cs` | Create |
| `docs/design/test-ui/pages/case-details--*.html` | Regenerate only an existing state this diff changes (wrapper correction; capacity-one lease) |

Do not change migrations, Core, Infrastructure, CSS, JavaScript, report
templates, old Assessment-route tests, fee-note preview, image curation,
damage-map work, sign-off writers, `catalogue.json`, or any new
`docs/design/test-ui/**` state. `AssessmentReportDraftWebTests.cs` remains
unchanged: [[ENG-034]] owns its Case-host retarget.

## Vocabulary boundary

Today, the editor can bind these established paths:

- `assessment.outcome`, `assessment.category`, and
  `assessment.salvage_value`;
- `narrative.engineers_comments` and `narrative.history_check`;
- `fee.agreed_fee` and `fee.description_lines`;
- legacy `costs.recovery_charge`, `costs.storage_charge`, and
  `costs.repairer_vat_registered`.

The legacy names must not be relabelled as D41 fields without [[ENG-035]]'s
merged contract: storage charge is not necessarily storage per day, and
repairer VAT is not claimant VAT. Recovery may use the existing path only if
that contract confirms the semantic match.

The D41 controls for excess, settlement betterment, claimant VAT registered,
reserve, repair duration, repair delays, report delay, storage per day,
recovery where newly defined, hire start, daily hire cost, diminution, and
salvage logistics render only after their [[ENG-035]] vocabulary constants
exist. No control is rendered as an inert or disabled placeholder.

The existing outcome set already contains Total loss, Repairable, Cash in lieu,
and Contract repair. Category and salvage value are shown for Total loss,
which is the current Core-required condition; `AssessmentPolicy` remains the
sole validation owner.

## Steps

1. Acquire the `OperatorLabels.cs` shared lock after [[ENG-034]] has released
   it, and add only editor vocabulary absent from
   `CaseWorkspace.EngineerSections`: `Excess`, `SettlementBetterment`,
   `ClaimantVatRegistered`, `Reserve`, `RepairDuration`, `RepairDelays`,
   `ReportDelay`, `StoragePerDay`, `HireStart`, `DailyHireCost`, `Diminution`,
   `SalvageLogistics`, `RepairCost`, `Equity`, `FinancialRatios`, and
   `Readiness`. Reuse ENG-034's existing section, outcome, salvage, recovery,
   report, signatory, fee, draft-action, and not-recorded labels.

   Files: `src/Pegasus.Web/Presentation/OperatorLabels.cs`.

2. After the explicit whole-file hand-off, inject `ISaveAssessment` into
   `DetailsModel` and add one
   `OnPostSaveAssessmentAsync` handler. It accepts the Case ID, expected
   version, operation key, reason, edit-lease token, validated section target,
   and the posted scalar values; it builds exactly one `SaveAssessmentRequest`
   with only the fields submitted by the Settlement or Report form.

   Reuse `CaseMutationPageModel.NewOperationKey`,
   `ExecuteCaseCommandAsync`, and `HandleLeaseFailure`. Preserve the existing
   expected-version, actor, operation-key, and edit-lease protections. The
   handler restores the submitted section after the command result, redirecting
   to `/Cases/{id}?section=settlement` or `/Cases/{id}?section=report`.
   A successful `EfCaseAssessmentStore` save clears the shared Case lease, so
   the PRG reload has no stale token and normal edit-mode acquisition resumes.

   Files: `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`.

3. Replace the two ENG-034 display-only bodies with Case-page forms that post
   the single assessment handler and share the one `Model.LeaseToken`. Reuse
   the Case partial panel/form convention, the Case-frame projection supplied
   by [[CASE-038]], `AssessmentVocabulary`, and `OperatorLabels`.

   Settlement shows the four Core outcome options, conditionally shows
   total-loss category and salvage value, and binds only vocabulary constants
   available after [[ENG-035]]. It displays repair cost from the Current
   estimate through Core's `EstimateTotals.Compute`; equity and financial
   ratios are displayed only from the Core-owned values exposed by [[ENG-035]].
   Razor and JavaScript perform no financial or readiness calculation.

   Report binds comments, history check, agreed fee, and fee description
   lines. It displays the [[CASE-040]] Case sign-off Engineer read-only; it
   does not add a selector or second writer. [[PLAT-068]] and [[CASE-040]]
   remain responsible for offering only flagged accounts and setting the Case
   field. It reuses `AssessmentReportProjection.Prepare` to render each
   outstanding requirement by name, never a completeness percentage. It
   reuses ENG-034's Generate and Preview handlers; report-image readiness and
   the fee-note preview remain absent for [[ENG-031]] and [[DOCS-018]].

   Both partials omit mutation forms when `AssessmentIsReadOnly` is true.
   They contain no explanatory copy; state labels continue to come from
   `OperatorLabels.CaseStage`.

   Files: `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml`,
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml`.

4. Create Case-host integration coverage using
   `AssessmentWorkspaceTestData.Create`, `FakeGetAssessmentAccess`,
   `IntakeWebApplicationFactory`, and the
   `AssessmentReportDraftWebTests` request/antiforgery pattern. Follow the
   local `RecordingStores` capture pattern where request assertions need a
   substituted store; it is private to the estimate test and is not reused
   directly.

   Prove Settlement and Report posts produce the expected single
   `SaveAssessmentRequest`, preserve version/operation-key/lease inputs,
   PRG to their respective section, and display the saved values through the
   Case-host preview path. Prove the four outcomes and total-loss-only salvage
   controls, D41 controls only after their constants are available, named
   readiness without a percentage, read-only omission of mutation forms, and
   read-only Case sign-off display. Dependency tests, not this suite, prove
   account filtering and sign-off assignment.

   Files: `tests/Pegasus.IntegrationTests/CaseAssessmentEditorsWebTests.cs`.

5. Run the verification commands, run the simplification pass over the
   branch's own diff (reuse, simplification, efficiency, altitude) and
   record its findings and dispositions below, confirm no modified path
   belongs to another lane, write the post-implementation report, and open
   the PR to `dev`.

   Files: no additional owned files.

## Acceptance checks

- Every supported Settlement and Report field round-trips through
  `ISaveAssessment` and reaches the Case-host report preview after its owning
  dependency is merged.
- The report lists each outstanding readiness requirement by name and never
  renders a percentage.
- Total-loss salvage requirements remain enforced by Core; all four established
  outcomes are selectable.
- Derived settlement figures come from Core-owned calculations only.
- A successful save clears the Case lease; stale or refused saves use the
  existing Case mutation failure handling.
- Sign-off is read from the [[CASE-040]] Case field, with account eligibility
  supplied solely by [[PLAT-068]].
- Complete cases render these sections read-only with no submitted mutation
  controls.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

No migration is in scope, so `./scripts/Test-MigrationGrants.ps1` is not run;
if a migration becomes necessary, stop and hand it to the serialized
migration lane. If `-Verify` reports a change to an existing
`case-details--*` state caused by these two bodies, commit that regenerated
snapshot in this PR under the capacity-one `docs/design/test-ui/**` lease
(wrapper correction); new Case-record states and their catalogue entries
stay with [[UIIMP-014]] — stop for its hand-off rather than adding them.

## Design rules that bind

- D30 in FRD-12: Settlement and Report are always-viewable Case sections;
  once Complete they are read-only — `AssessmentIsReadOnly` (from
  `AssessmentAccessPolicy.IsReadOnly`) makes every mutation control
  absent, not disabled; `CanOpen` never decides whether a section renders.
- D41: the settlement field set; financial ratio lines are permitted; the
  "no percentage" rule (D23) is about completeness only — readiness is a
  named list from `AssessmentReportProjection.Prepare`.
- D31 / DOCS-017: the sign-off Engineer is a Case field with one writer
  (CASE-040); ENG-029 displays it and adds no second list or writer.
- D42: the fee-note preview is DOCS-018's; ENG-029 edits the agreed-fee
  inputs only.
- `docs/design/README.md` § Voice and § No explanatory copy: labels,
  values and controls only; no hint, empty-state panel or how-it-works copy.
- Labels live only in `Presentation/OperatorLabels.cs`; exact Case state
  wording comes from `OperatorLabels.CaseStage`; reuse ENG-034's
  `CaseWorkspace.EngineerSections` keys and add only what the editors need.
- D7 / D21 and § Absent versus disabled: an excluded or not-yet-available
  field (a D41 path whose vocabulary constant does not exist) is absent,
  never drawn disabled; a control disabled by a state gate needs a real
  handler and a non-empty `data-condition`.
- `docs/engineering.md` § One Core owner: no repair-cost, equity, ratio or
  readiness calculation in Razor or JavaScript; `AssessmentPolicy` is the
  only validation owner.
- D43 is not authorisation to copy mockup fixture values into tests.

## Stop condition

Open the ENG-029 PR against `dev` with all owned-path tests and required
verification passing, move the ticket to Review, and do not merge it, write
proof, or start neighbouring-ticket work.

## Simplification pass

(Written by the implementer after the diff exists: dated heading, the four
lenses, findings and dispositions.)
