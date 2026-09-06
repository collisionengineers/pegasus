# Stream C → Stream A (Foundation) handoff inventory — 2026-09-06T06:10Z

Compiled from the Wave 0 PR dispositions (`scratch/pr-639-preservation`, `scratch/pr-646-disposition`, `scratch/pr-671-disposition` on this ticket). These are exact requests for A-owned files; C makes none of these edits.

## C-F01 (PR 639 / PR-069 watermark)

| Item | Value |
| --- | --- |
| Entity / table | `UnidentifiedItemEntity` (`src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs`, after `ResolutionTargetReference` D:24) / `dbo.UnidentifiedItems` |
| Field | `ReconciledAssociationVersion`, `long?` → `bigint NULL`; NULL = resolved, never rechecked; no default, no backfill, no index, no fluent config, not on the domain record |
| Migration | name `UnidentifiedResolutionRecheckWatermark`, regenerated on D after `20260905010654_CaseSignOffEngineer` (discard branch timestamps `20260829222702` / `20260902030930`); `Down` drops the column |
| Grant | no new GRANT; keep the SQL-Server-only assertion that `pegasus_worker_runtime_role` holds object-level (`class = 1`, `minor_id = 0`) UPDATE on `dbo.UnidentifiedItems` in state G/W, else `THROW 51000` |
| Census | append the regenerated id to `CommittedMigrationCreatesTheSqlServerSchema` (`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` D:120–124) |
| Worker | `src/Pegasus.Worker/IntakeFunctions.cs` D:193–197 and `[LoggerMessage]` D:273–279 gain the `{Corrected}` placeholder from the 4-arity `ReconcileUnidentifiedDestinationsResult` C will publish; same timer, no new schedule |
| Open risk | recheck query joins `UnidentifiedItems.OriginId` = `IntakeManualAssociations.IntakeReceiptId`, filtered on State/ResolvedByActorKind/ResolvedByActorSubjectId/OriginKind and watermark inequality, ordered `ResolvedAtUtc, Sequence`, Take 50, 10-second timer — confirm index coverage against D before closing C-F01 |

## PR 646 residual (no schema)

| Item | Value |
| --- | --- |
| Shared test support | `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` D:725–753: extract `DispatchNextAsync` and add `internal static Task<(QueuedIntakeStatus Status, IntakeEvaluationRevision? Evaluation)> DrainStagedToTerminalAsync(services, stagedReceiptId, ct)` exactly as at `32a5a62ce` (head 725–786); an earlier over-broad variant broke the full suite. C's `ProviderApiSubmissionTests` addition depends on it |
| Docs | `docs/frd/frd-09-provider-and-intermediary-routes.md` D:41–42 rewording and the 9-line "Existing-Case rejection" bullet after D:139 (`[[AUTO-017]]` forward ref) — root/Foundation |
| Narrative | TICK-058 plan/report say the Provider API "remains disabled"; it is live since release 37 (`Features__ProviderApi` true, no credential issued). Correct the ticket documents; C will not repeat the claim |
| Follow-up | OWNER 2026-09-03 request to make provider claim ref required — new ticket beside AUTO-017, not part of the port |

## C-F06 (PR 671 Image Intake principal)

| Item | Value |
| --- | --- |
| Entity | `ImageIntakeEntity` (`ImageIntakeEntities.cs`, after `ImageIntakeReference` D:27): `Guid? PrincipalId`, `PrincipalEntity? Principal` |
| Config | `PegasusDbContext.cs` `ImageIntakes` block: `HasIndex(PrincipalId)` (D:692–697), `HasOne(Principal).WithMany().HasForeignKey(PrincipalId).OnDelete(Restrict)` (D:703–710) |
| Migration | regenerate at D: `AddColumn<Guid>("PrincipalId","ImageIntakes","uniqueidentifier", nullable: true)`, `CreateIndex("IX_ImageIntakes_PrincipalId")`, `AddForeignKey("FK_ImageIntakes_Principals_PrincipalId", → Principals.Id, Restrict)`; no backfill; Down drops all three; append to the census |
| Schema tests | `IntakePersistenceIntegrationTests.cs` image-intake block (~D:169–175): `sys.columns` nullable and `sys.foreign_keys` `NO_ACTION` assertions |
| Shared test support | `IntakeWebTestSupport.cs`: optional `DbCommandInterceptor? commandInterceptor` ctor parameter wired in `ConfigureWebHost` by replacing the host's `IDbContextFactory<PegasusDbContext>` (not the private schema-management provider) — hunks at `743311a0` :10, :38, :73, :200. C's read-count test needs it |
| Grants | branch says none needed (runtime roles already hold UPDATE on `ImageIntakes` per `scripts/Invoke-AzureDatabaseBootstrap.ps1:313–317` and SELECT on `Principals` per `Migrations/20260729199000_RuntimeRoleReconciliation.cs:252,:289`) — please re-verify at D |
| B dependency | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:560–589` `ImageRow` principal fact/subtitle is B's; C's `ImageIntakeSummary.PrincipalCode` is its only prerequisite |

## Ordering

C's C14-style LEFT JOIN projection and the recheck store methods compile only after A1/A3 and the watermark field exist at F (or a later common G). C will not begin C01/C07 persistence until those are published.

## Addendum 06:16Z — C03 composition window must include two A-owned test/reference edits

1. `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` (A-owned) `IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary` (D:192–208) asserts that `IInstructionExtractionPolicy` has exactly one Core implementation (`QdosInstructionExtractionPolicy`) and that `ProcessIntake`'s single constructor takes `IInstructionExtractionPolicy` by type. The first new profile class on the C head fails it. The A-authored C-F03 DI patch therefore has to carry the rewrite of that test (new invariant: one Core `InstructionExtractionPolicySelector`, N profiles, no concrete profile type in `ProcessIntake`'s constructor, QDOS still registered). C will publish the concrete selector/profile types and their tests first, per the coordination gate, and record the head.
2. `reference/workproviders-and-repairers/principal-identification-corpus.v1.json` is `Closed` in the register (the C stream plan lists it as a C existing file — register wins). `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs` `TrackedPegasusSourceHashesHaveNotDrifted` (D:291) pins the hashes of `QdosMailRoutePolicy.cs` / `QdosInstructionExtractionPolicy.cs`, and C03/C04 must edit both. Regeneration is the A-owned `scripts/Build-PrincipalIdentificationCorpus.ps1` (+ `scripts/reference_data/build_principal_identification_corpus.py`). Request: A re-runs the corpus build against the recorded C head in the same serialized window as the DI patch (or grants C a one-time scoped edit of the tracked-hash entries). The corpus `runtimeContract.loadedByRuntime=false` and QDOS-only `runtimeActive` assertions stay true: C03 document profiles are selected by `AnalyzeRetainedInstruction` and never activate a sender route.

A01 -> C08 shared-label cleanup request (A leaves C-owned files untouched): account periodicreview is removed on A. Remove unused OperatorLabels.StaffAccounts.ReviewDue and its XMLcomment referencing StaffAccessReviewProjection, plus StaffAccounts.Review (after confirming no remaining Ccaller). In Pages/Shared/_StatusChip.cshtml remove obsolete `// Access review` and `due no review recorded` mapping; `due`/`recorded` may have other real callers, retain onlyifactualcurrentuse requires. No AdminNav Accesslink exists already, so no navchange needed for this removal. A01 removes Access and unlinked Accounts/Edit routes withoutaliases; Accounts/Index + Confirm hostallrealactions. A will update sharedcanonical FRD/capability/operator decision wording atA08. A07/combined snapshots will verify routedremoval onceC shellchanges settle. G1 additionallyprovides ICaseEngineerChoices.GetAsync(ActionActor,CancellationToken) returning CaseEngineerChoice(StaffId,DisplayName); A01 backend will implement enabledEngineer options without signatoryimage requirement for C07.

A01 now implements G ICaseEngineerChoices on existing EfStaffAccountQueries, authorized PerformCasework, enabled Engineer-role accounts only, stable username+Id ordering, no signatory/signature requirement; DI maps existing scoped instance. This is branch-local A work not yet validated/published; C can use contract/fake until recorded A domain commit. G1 published ef3c8dbd0cde88aca661d181497599e460fe1d0f; see scratch/execution for exact merge instructions.
