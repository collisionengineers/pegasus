# PR 646 — behavior diff and hunk disposition (Stream C, C01 item 6)

Read-only analysis (Wave 0, 2026-09-06, Opus 5 under claude-fable-c); git reads issued from `.worktrees/tick-058`.

## 1. Header

| Field | Value |
| --- | --- |
| PR | #646 "Reject Provider API matches to existing Cases (TICK-058)", head ref `TICK-058-verification-plan-remediation`, base `dev` |
| Recorded tip / live head | `32a5a62ce4f13baba45a0bad06df5498f38dcd19` — identical, no drift |
| State | OPEN / MERGEABLE |
| D | `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2` |
| Merge-base | `cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`; exactly one commit on the branch (`32a5a62ce` "Reject provider submissions matching existing cases") |

Diffstat `D...32a5a62ce` (7 files, +252/−30): `docs/frd/frd-09-provider-and-intermediary-routes.md` (+14), `src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs` (+53), `src/Pegasus.Core/Intake/DurableIntake.cs` (+1), `src/Pegasus.Core/Intake/ProcessIntake.cs` (+48), `tests/Pegasus.Core.Tests/Intake/CaseMatching/EvaluateIntakeCaseMatchTests.cs` (+24), `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` (+71), `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` (+71).

PR conversation (2 comments, 0 formal reviews): (1) 2026-09-02 OWNER "Independent Kanmer review — needs changes (F-001, major)" — the production diff correctly implements create-only API-01; the blocker is a false narrative claim that the Provider API "remains disabled" whereas `infra/modules/platform.bicep` sets `Features__ProviderApi` true and docs record it live since release 37 with no credential issued. No code hunk implicated; C must not repeat the "disabled" claim. (2) 2026-09-03 OWNER: "Require provider claim ref … will set as required field for API. Date of incident is unreliable for same-day accidents." Forward scope change, absent from this diff (`ClaimNumber` optional at head); route as a follow-up alongside `AUTO-017`, do not absorb into C01.

Kanmer TICK-058: `status: review`, `review_round: 1`, area automation-integrations, `prs: ['594','646']`, `delivery_state: integrated`, `delivery_sha: 0d985c9e…`, `blocks: TICK-060`, `refs: docs/frd/frd-09-provider-and-intermediary-routes.md`.

## 2. Already at D versus residual

All seven touched files are identical between the merge-base and D, so 100% of the delta is residual; nothing was absorbed. The branch does not touch `src/Pegasus.Core/ProviderApi/**`, `src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs`, `EfProviderSubmissionStore.cs`, `tests/Pegasus.Core.Tests/ProviderApi/**`, DI, DbContext or any migration — no schema or DI work.

Everything the residual depends on already exists at D: `CaseMatchSourceData` (CaseMatchContracts.cs:29–33), `CaseMatchIndexKeys` (:35–40), `IProviderCaseMatchPolicy.DeriveIndexKeys` (:87), `QdosCaseMatchPolicy.WorkProviderCode => "QDOS"` (:20, :88), `ICaseMatchCandidateQueries.FindByAnyKeyAsync` (:60–63), `CaseMatchOutcome {UniqueMatch, NoMatch, NoKeys, Ambiguous}` (:90–95), `EvaluateIntakeCaseMatch` eliminator and `RedirectCreatedInErrorAsync` (EvaluateIntakeCaseMatch.cs:13–113), `ProcessIntake` ctor param `caseMatchEvaluator` (ProcessIntake.cs:18), `ProviderSubmissionBinding` (ProviderSubmission.cs:100–104), optional `ProviderInstruction.ClaimNumber/ClaimantName/VehicleRegistration/DateOfIncident` (ProviderInstructionPolicy.cs:12–34), `IntakeAssessment` trailing `CaseMatchDecision` (ProcessIntake.cs:1073–1086), `TerminalInputFailureCode` switch (DurableIntake.cs:1059–1065), failure-code surfacing `receipt?.FailureCode ?? status?.FailureCode` (ProviderSubmission.cs:597) and HTTP projection (ProviderApiEndpoints.cs:33, :257), `AssociateCaseIfUnambiguousAsync` gated on UniqueMatch (DurableIntake.cs:926–935), test helpers in ProviderApiSubmissionTests.cs and `IntakeWebDriver.DrainStagedAsync` (IntakeWebTestSupport.cs:720–765). `AUTO-017` exists as a Kanmer folder but not under `docs/`.

## 3. Hunk disposition table

| # | File | Hunk | Behavior | Disposition |
| --- | --- | --- | --- | --- |
| H1 | `docs/frd/frd-09-provider-and-intermediary-routes.md` | D 41–42 → head 41–43 | Reworded: definitive provider-API instruction follows the same new-Case creation policies; "API-01 is create-only; it never associates material with or mutates an existing Case." | owner-A (root/Foundation docs) |
| H2 | same | 9 lines after D 139 | New "Existing-Case rejection" bullet: policy applied to declared claim number, VRM, claimant, incident date; unique-or-ambiguous fails with `provider_existing_case_match`, allocates no Case/PO, no association/mutation; no match ⇒ ordinary creation; API updates deferred (`[[AUTO-017]]`) | owner-A |
| H3 | `EvaluateIntakeCaseMatch.cs` | after D 48 (head 49–99) | Body from the keys null-check onward moves verbatim into private `EvaluateAsync(workProviderCode, policy, keys, ct)`; new `public Task<CaseMatchEvaluationResult?> ExecuteDeclaredAsync(string workProviderCode, CaseMatchSourceData sourceData, CancellationToken)` resolves the policy by ordinal `WorkProviderCode`, returns null when none owns the code, calls `policy.DeriveIndexKeys(sourceData)` and widens `CaseMatchIndexKeys` positionally into `CaseMatchKeys`. No new grammar, no second eliminator | retain-C-port |
| H4 | same | D 64 → head 115 | `FindByAnyKeyAsync(workProviderCode, …)`; mail path passes `route.WorkProviderCode` so email behavior unchanged | retain-C-port (inseparable from H3) |
| H5 | `DurableIntake.cs` | after D 1063 | `ProviderExistingCaseMatchException => ProviderExistingCaseMatchException.FailureCode` in `ProcessQueuedIntake.TerminalInputFailureCode` — terminal on first attempt, code stamped on queued status | retain-C-port (FYI to A: durable worker file, C-owned path) |
| H6 | `ProcessIntake.cs` | D 498–511 → head 498–533 | (a) `binding is null` ternary → `if` block (pure refactor); (b) `caseMatchEvaluator.ExecuteDeclaredAsync(binding.PrincipalCode, new(instruction.ClaimNumber, instruction.VehicleRegistration, instruction.ClaimantName, instruction.DateOfIncident), ct)`; (c) `if (providerMatchDecision?.Outcome is UniqueMatch or Ambiguous) throw new ProviderExistingCaseMatchException();` (d) otherwise decision threaded into `DeclaredAssessment` | retain-C-port |
| H7 | same | D 756 → head 778–779 | `DeclaredAssessment` gains `CaseMatchEvaluationResult? caseMatchDecision` | retain-C-port |
| H8 | same | D 798 → head 821–823 | constructed `IntakeAssessment` ends `…, null, null, caseMatchDecision)`. Side effect: NoMatch/NoKeys provider submissions now persist an `IntakeCaseMatchDecisions` row and case-match telemetry (EfIntakeReceiptStore.cs:105–130, EfIntakeMutationStore.cs:982–991, ProcessIntake.cs:224–256) where D persisted none; association not enabled (requires UniqueMatch, which throws first) | retain-C-port; declare the side effect in the proof |
| H9 | same | appended after D 1096 | `public sealed class ProviderExistingCaseMatchException() : Exception("The provider submission matches existing Case work; API-01 cannot update it.")` with `public const string FailureCode = "provider_existing_case_match";` in namespace `Pegasus.Core.Intake` | retain-C-port; optional C-owned relocation into `IntakeContracts.cs` |
| H10 | `EvaluateIntakeCaseMatchTests.cs` | after D 10 | `DeclaredIdentityUsesTheProvidersExistingNormalizationAndEliminator`: stub `DerivedKeys = ("12345/1","AB12CDE","SMITH","J",null)`; `ExecuteDeclaredAsync("QDOS", ("AB/12345/1","AB12 CDE","Jane Smith",null))` → UniqueMatch, `MatchedCaseId == CaseA`, `Keys.DurableClaimToken` equals the derived token | retain-C-port; move the `[Fact]` below all field declarations (branch inserts it between `CaseB` and `CaseC`) |
| H11 | same | D 404–406 | `StubPolicy.DerivedKeys` init property; `DeriveIndexKeys` returns it (default all-null) | retain-C-port |
| H12 | `IntakeWebTestSupport.cs` | D 725–753 → head 725–786 | `DrainStagedAsync` retry logic extracted into `DispatchNextAsync`; new `internal static Task<(QueuedIntakeStatus Status, IntakeEvaluationRevision? Evaluation)> DrainStagedToTerminalAsync(services, stagedReceiptId, ct)` pumping until Complete or Failed | owner-A (shared test support); C's H13 depends on it; hand A the `32a5a62ce` version (an earlier over-broad variant broke the full suite) |
| H13 | `ProviderApiSubmissionTests.cs` | after D 189 (head 191–261) | `ASubmissionMatchingAnExistingCaseIsRejectedWithoutMutationOrDuplicateAllocation` | retain-C-port |

No hunk is owner-B or reject.

## 4. Residual behavior C must implement

Invocation: in `ProcessIntake.AssessAsync` (D:439) inside `if (sourceChannel == IntakeSourceChannel.ProviderApi)` (D 491), after `FindProviderBindingAsync` returns a binding and before `DeclaredAssessment` (so before any draft, review fields, completeness or allocation). First argument `binding.PrincipalCode` (authenticated Principal code on the retained submission row, ProviderSubmission.cs:103) matched ordinally to `IProviderCaseMatchPolicy.WorkProviderCode` — the assumption that Principal code and work-provider code share one vocabulary holds for QDOS; C should state or assert it. Second argument is declared facts only (`ClaimNumber, VehicleRegistration, ClaimantName, DateOfIncident`), never file contents. Normalization via `policy.DeriveIndexKeys` (same as the write side, CaseMatchEntities.cs:107), not `ExtractMatchKeys`. Eliminator reuse: both paths tail-call the same `EvaluateAsync` (HasAnyKey ⇒ NoKeys; FindByAnyKeyAsync; per-candidate Evaluate; RedirectCreatedInErrorAsync; survivors 0 ⇒ NoMatch, 1 ⇒ UniqueMatch, >1 ⇒ Ambiguous; `qdos_case_match`/1). Null return when no policy owns the code ⇒ ordinary create proceeds.

Unique = exactly one eliminator survivor after CreatedInError redirect; Ambiguous = more than one; terminating predicate exactly `providerMatchDecision?.Outcome is UniqueMatch or Ambiguous` (null/NoMatch/NoKeys do not terminate). Mechanism: `ProviderExistingCaseMatchException` thrown out of `AssessAsync`; `ProcessQueuedIntake` catches and `TerminalInputFailureCode` maps it to `provider_existing_case_match` with `terminal: true` (DurableIntake.cs:1040–1051). Note: D's `ExecuteCoreAsync` calls `AssessAsync` at :185 outside any try, so on the direct `ProcessIntake.ExecuteAsync` path the exception would propagate; whether any ProviderApi path reaches that entry point is undetermined read-only.

Testable statements: (1) first declared instruction → 201, one Case and one `CaseIntakeLinks` row; (2) duplicate under a different idempotency key is still 201 with its own submissionId (receipt first, per FRD-09); (3) staged receipt retained and discoverable via `IIntakeWorkStore.FindBySourceIdentityAsync(new(ProviderApi, ProviderSubmissionPolicy.SubmissionToken(id)))`; (4) `DrainStagedToTerminalAsync` reaches `Failed` with null evaluation; (5) `GET /api/provider/v1/submissions/{id}` → 200, `status == "Failed"`, `failureCode == "provider_existing_case_match"`, `caseReference` null (unchanged D code); (6) `Cases.Count == 1`; (7) `CaseIntakeLinks.Count == 1`, existing Case untouched; (8) Ambiguous takes the identical path — but PR 646 has no API-level ambiguous test; C must add one; (9) terminal, not retried; (10) unit normalization proof (H10); (11) new side effect: NoMatch/NoKeys persist an `IntakeCaseMatchDecisions` row and telemetry.

The exact code string is `provider_existing_case_match`, one const, never duplicated as a literal.

## 5. Hunks that go to A / root

H1, H2 (`docs/frd/frd-09…`) → root/Foundation. H12 (`IntakeWebTestSupport.cs` `DispatchNextAsync` extraction + `DrainStagedToTerminalAsync`) → A, must land before or with C's H13. No Stream B hunk. Non-hunk items: correct the "disabled" narrative in TICK-058's plan/report (root); the "require provider claim ref" API change is a new ticket beside `AUTO-017`.

## 6. Undetermined read-only

(1) Whether `ProviderSubmissionBinding.PrincipalCode` equals a `WorkProviderCode` for every Principal (verified for QDOS only). (2) Whether any ProviderApi source can reach the direct `ProcessIntake.ExecuteAsync` entry point. (3) Whether any existing test drives Ambiguous through the API (none in this diff). (4) TICK-058 sub-folder contents were not read. (5) The PR body's build/test counts are the author's claim, not re-run.
