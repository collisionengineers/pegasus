# Post-implementation report — ENG-029

## Result and boundaries

The existing Case Save now records the declared Settlement, Report and Vehicle
History edits through one ISaveCaseWorkspace command and transaction.
Engineering writes retain their Core role/state rules; ordinary Case facts
keep their existing pre-handoff save path. The source/runtime change is ready
for independent review, not Done or deployed.

Author baseline: aefe4c32d078ad79c0368666b5666032e6865248.
Publication packet resolved dev96777888bfa7ee7f85d63979a4a09ae10cda7d13.
Exact retained branch/worktree: ENG-029-case-workspace-editors,
.worktrees/eng-029. Approved plan a940dcb4f0bb7d81, file map
9a7f6a62591c25fe. Source checkpoint and normal dev integration are recorded
below once completed; neither shared ref is changed by this publication.

Root and independent reviewer expressly permit source/runtime integration
before manual visual acceptance. The 1580/1100/760 editable/read-only/conflict
inspection remains INCONCLUSIVE because browser local-file navigation was
refused by URL security. No workaround, no visual waiver and no Done claim.
Keep that acceptance unchecked through Verifying. Automated snapshot checks
are not substituted for manual visual review.

## Implementation and real callers

Details.OnPostSaveAsync replaces its ISaveCase call with the existing
ISaveCaseWorkspace owner. Optional canonical sections bind native controls
associated with case-edit-form. Unposted sections stay untouched; unshown
accepted Facts/Confirmed values, typed costs, dates and source metadata are
preserved. Suggestions are not silently adopted. Submitted expectedVersion,
lease, operation key and reason remain the caller's original intent.

Canonical engineering field posts use AssessmentAccessPolicy; typed invalid
values and unknown fields refuse the whole Save. The dictionary uses the
existing MVC assessmentFields prefix explicitly. Existing bounded
current-versus-proposed retention handles clears, false and safe signer display,
never tokens. The actual ExecuteCommandAsync authorization refusal retains
proposals while clearing edit authority and returning Forbid.

CaseDataFieldWriter.SetConfirmed is shared by workspace Save and the existing
SaveCase/MCP writer. Equal accepted Fact/Confirmed type/value is now a no-op;
actual change, explicit clear and explicit chosen suggestion keep the original
writer. Original provenance is preserved instead of inventing staff confirmation.

Case GET uses the existing metadata-only ICaseReportSnapshotSource and
CaseReportReadiness, eligible sign-off source and Core settlement projection.
AssessmentReportProjection.BuildSettlement is the one calculation for the
page and report: missing accepted Current estimate/value withholds derived
figures, not zero facts. RepairDays remains Estimate-owned. Report preview,
generation, custody, delivery and TICK-085 import retain their existing owners.

FRD-06/11 and the directly affected design paragraphs match this one-save
contract. No new route, DI registration, schema, package, CSS, JS, generic
form framework, external service or business vocabulary was added.

## Exact changed files and purpose

| Path | Change |
| --- | --- |
| src/Pegasus.Core/Assessment/AssessmentContracts.cs | Remove unused duplicate settlement repair-duration declaration/definition. |
| src/Pegasus.Core/Assessment/AssessmentPolicy.cs | Admit only the already supported PostReport write state. |
| src/Pegasus.Core/Reports/AssessmentReportProjection.cs | Reuse the existing pure settlement calculation with incomplete-input safety. |
| src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs | Equal accepted Fact/Confirmed preservation in SetConfirmed only. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs | Direct-caller denial wording only. |
| src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs | Direct-caller denial wording only. |
| src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs | Direct-caller denial wording only. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | One workspace save, typed sections/authority and metadata readiness. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Existing safe proposed-value retention, including actual Save authorization refusal. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml | Native form-associated editors and Core-derived read-only values. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml | Native report controls; preserve other accepted report actions. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml | One Vehicle History editor/value in Vehicle. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | Existing common form controls and remove obsolete forced-demotion warning. |
| src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | One presentation map reused by binding/rendering/retention. |
| tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Existing policy and state/dead-path assertions. |
| tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs | Same complete settlement projection, missing/unconfirmed/current-input guards. |
| tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Update existing actual Save consumer/fake and retain authority assertions. |
| tests/Pegasus.IntegrationTests/CaseEditModeWebTests.cs | Partial CaseDetails fixture: fields/authority/invalid values/unknown keys/proposal guards. |
| tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs | Existing metadata source and valid accepted estimate totals fixture. |
| tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs | Only handed-off equal-name/VRM expectations preserve original Facts. |
| tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs | Actual SQL combined saves, replay, rollback, invalidation and accepted provenance. |
| tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs | Metadata-only GET and persisted workspace fields reaching actual preview. |
| docs/frd/frd-06-vehicle-and-engineering-evidence.md | Current field/repair-day writer contract; preserve TICK-085. |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Report/Vehicle writer, readiness, signer and date behaviour. |
| docs/design/README.md | Existing one-Save sections and warning wording; preserve DOCS-019. |
| docs/design/test-ui/pages/case-details--default.html | Actual scoped normalized snapshot delta. |
| docs/design/test-ui/pages/case-details--conflict.html | Actual scoped normalized snapshot delta. |

The generator also refreshed index.html and case-details--unavailable.html
locally, but Git's normalized diff is empty for both. No unrelated route is
included. Root authored the five Core/test files and two SQL/report fixture
files in the same worktree; pack_reconcile authored the remaining scoped
change. principal_delivery_audit is the independent whole-ticket reviewer.

## Runtime evidence — root executed, author inspected

Windows/PowerShell 7, this exact author worktree. Root was the sole heavy
verifier. Author performed only source/Git, TRX/hash and board-document checks.
Native setup/build exact start instants were not recorded; no timestamps are
invented. Times below for test runs are the TRX's precise UTC values.

| Attempt | Actual result |
| --- | --- |
| Root72086: locked solution restore, then full solution Release build --no-restore | PASS exit0; seven projects restored; build63.93s,0warnings/errors. |
| Root42335 Core | PASS100/100,0skip,131ms test duration. TRX05:59:53.5796672Z–05:59:55.1706858Z. |
| Root42335 initial Integration | FAIL exit1;38PASS/21FAIL/0skip,59total,1m57s reported duration. TRX05:59:56.5356676Z–06:01:56.3415878Z. |
| Root52531 corrected Integration project Release build --no-restore | PASS exit0,54.67s,0warnings/errors; unchanged Core not rerun. |
| Root48296 corrected 18-method selection | PASS exit0;31/31,0skip,2m16s reported duration. TRX06:33:05.7876076Z–06:35:25.1320806Z. |
| Root59742 scoped snapshot update -SkipCapture | PASS3checks,exit0. |
| Root38933 scoped snapshot verify -Verify -SkipCapture and catalogue | PASS3checks; catalogue60routes/67prototypes/0broken,exit0, as reported by root. |
| Root CUA manual1580/1100/760 inspection | INCONCLUSIVE: browser URL security refused local-file navigation; no native-command exit code, no workaround. |
| Author final git diff --check | PASS exit0. CRLF normalization notices are not errors. |

TRXs are retained under artifacts/verification in the author worktree:

| Artifact | SHA256 |
| --- | --- |
| eng-029-core.trx | 397195ACE12E391DB5632B37A73A0138DCBDC09AA10740BDEB1439A280AB3FE9 |
| eng-029-integration.trx | BE35CA056D98924E3A02B3A1224172B110C99D84EE04CDE9F538F3C661F7CECD |
| eng-029-integration-corrected.trx | 045402CA0DBE8570923D8AF88CEDA07FE4F5F742BA57F44664F790977D0FF65A |

The author read all three actual counter/time/class records and rehashed files.
Corrected tests include all21 initial failures plus actual SQL provenance,
SaveCase consumer, unknown-field/authorization/proposal guards and the three
canonical capture inputs. Do not total overlapping runs as unique tests.
No unchanged100Core or whole59Integration rerun was performed.

Root retained initial failed-run captures at
artifacts/test-ui-capture-initial-failed before making the fresh canonical
artifacts/test-ui-capture directory. The script owns that directory; an
alternate environment directory was not a substitute. Old captures and failed
TRX are retained, not overwritten by the corrected pass.

## Failure causes and dispositions

- Accepted estimate fixture omitted RecordedTotals, causing GET500 when the
  newly shared projection legitimately required frozen totals. Fixed with
  existing EstimateTotals.Compute in the fixture; no production fallback.
- Authority test singleton resolved scoped staff query. Fixed scoped fixture
  registration; authorization and signer assertions retained.
- Optional dictionary used MVC empty-prefix fallback, treating ordinary form
  keys as assessment keys before Save/version checks. Explicit existing
  assessmentFields form prefix fixes the owner; unknown-key refusal stays.
- Two preview fixtures omitted required incident/instruction dates. Root
  records existing ReadyInput dates through real Overview save and uses the
  saved identity. No readiness guard or preview assertion weakened.
- Independent pre-publication finding: proposal retention was on unrelated
  ClaimLease catch, not actual Save. Moved to ExecuteCommandAsync; existing
  Forbid and lease clearing remain.
- Independent pre-publication finding: equal carried accepted values acquired
  new confirmation attribution. Root-approved shared writer no-op and real
  SQL provenance test close it. The two prior tests whose equal-Fact
  expectations required manufactured confirmation now assert whole original
  Fact/source and absent redundant Confirmed; all other assertions remain.

No runtime failure was silenced or reclassified as PASS. Historical review
dispositions remain in the versioned plan/research. A publication-plan write
using a mistyped project identifier was refused without change; fresh
project/version readback preceded the successful write. This was not a build
or product failure.

## Commands and smallest merged verification

Executed command forms were the existing solution locked restore and Release
--no-restore build, Core/Integration dotnet test --configuration Release
--no-build --filter with the exact selectors in scratch/verify, --logger
trx;LogFileName=<the unique names above>, --results-directory
./artifacts/verification. Correction compiled only the existing Integration
project. Scratch/verify retains the original and exact18-method correction
filters; it is the detailed command log, not another requirement owner.

For merged verification, reuse the named two Core classes and initial focused
Integration selector plus the correction-only method selectors, with precise
source/caller census to avoid a broad class auto-OR. Include both real SQL
provenance consumers and metadata/preview tests. The three actual capture
inputs are ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues,
CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey,
and TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor.
Run scoped Update-TestUiSnapshots -SkipCapture -Scope case-details, then
-Verify -SkipCapture for the same scope and Test-UiCatalogue. Preserve every
attempt. Root retains the final converged solution/release gate.

## Remaining acceptance and publication stop

Manual visual inspection remains required in Verifying at the named widths
and states. No full browser/accessibility, live provider, send, deployment or
operator acceptance is claimed. Preview tests genuinely persist Case and
assessment fields; Current estimate and sign-off profile remain explicit
existing fixture seams, not full persisted account-resolution proof.

Root authorized checkpoint followed by a normal clean-branch merge of
origin/dev96777888, preserving INTK-064's constructor alongside this ticket's
assertions. Stop and return that exact head/census before push/PR. No self
review, merge to dev, claim release or cleanup is authorized.
