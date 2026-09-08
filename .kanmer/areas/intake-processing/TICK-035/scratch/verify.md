Root correction verification 2026-09-07 23:52–23:54UTC: incremental Release solution build PASS56.38s0warn; InstructionReviewFieldTests9PASS/0skip. Five focused integration cases:3PASS2FAIL,42s. MP representative read.RequiresOcr assertion false at Top15InstructionCorpusTests614; original-email allocation now creates a Case but initial state expected not_ready actually review at QdosAllocationRecoveryTests93. No assertion changed merely to pass; author directed to inspect actual PDF qualification and original image assets/readiness before correction. Prior224/14/21 passing checks not rerun. TRXs tick-035-provenance-core.trx and tick-035-provenance-integration.trx retained under artifacts/verification.

## Exact merged acceptance started — 2026-09-08T02:22Z

Independent whole reviewdf2745313906828d read by root and exacthead/thread/gates/plan7a28b8ab58ed1ca2/timestamp/board synchronization checked. Guarded squash PR692 merged to dev at2026-09-08T02:20:31Z, exact56566371a5b80ef59c4f98e377c8e8ff6469b5f7. Ticket moved Review→Verifying with fresh gates and lease renewed30. Created clean detached .worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7 at that full SHA, same source common Git directory. Compared to reviewed cca5521a, only seven previously accepted ENG041 correction files differ (+210/-71); no claim of whole-tree author-test equality.

Root is sole heavy verifier. Run locked restore/full Release build and bounded existing Core owners, eight CaseMatch SQL callers, four genuine mail/replay outcomes including F003, declared API rejection, forward identities, receipt candidate roundtrips and the single fifteen-original identity/work-type/key method. No entire corpus/Browser suite or new Settings capture; those files are unchanged and prior exact source-bound evidence remains. Initial read-only guessed three test paths returned exit1 before execution; rg --files resolved their actual locations. Preserve all test attempts and unique output paths. No source changes, cloud/mail write or deployment.

## Exact root86956 commands

Cwd: .worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7.
Set PEGASUS_REFERENCE_PACK_ROOT=C:/Users/Alex/Documents/GitHub/pegasus/pegasus_pack; removed capture dir/scope/mode env entries for this process. No UI capture.

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ProcessIntakeTests|FullyQualifiedName~MailRoutePolicyTests|FullyQualifiedName~MailClassificationPolicyTests|FullyQualifiedName~CaseMatchPolicyTests|FullyQualifiedName~EvaluateIntakeCaseMatchTests|FullyQualifiedName~InstructionExtractionPolicySelectorTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests|FullyQualifiedName~ProviderInstructionPolicyTests' --logger 'trx;LogFileName=tick-035-merged-core.trx' --results-directory ./artifacts/verification
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseMatchIntegrationTests|FullyQualifiedName~InlineForwardedMailRouteTests|FullyQualifiedName~QdosAllocationRecoveryTests.GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions|FullyQualifiedName~QdosAllocationRecoveryTests.ClassificationNegativeAndAmbiguityFixturesPersistWithoutInventedCaseTypes|FullyQualifiedName~QdosAllocationRecoveryTests.PersistedStaffForwardRetainsOuterTransportAndOriginalQdosIdentity|FullyQualifiedName~CaseDataCompletenessPersistenceTests.IntakeFieldCandidatesRetainProvenanceAcrossReceiptPersistence|FullyQualifiedName~ProviderApiSubmissionTests.ASubmissionMatchingAnExistingCaseIsRejectedWithoutMutationOrDuplicateAllocation|FullyQualifiedName~Top15InstructionCorpusTests.OneGenuineInstructionPerPrincipalProvesSelectedWorkTypeAndMatchKeys' --logger 'trx;LogFileName=tick-035-merged-callers.trx' --results-directory ./artifacts/verification
git diff --check
git status --short --branch
git rev-parse HEAD
```

Every command exited0; exact final HEAD56566371a5b80ef59c4f98e377c8e8ff6469b5f7; clean detached. Whole guarded script returnedexit0. Issued during02:22UTC minute; precise restore/build start instants were not separately captured. Restore7 projects≤1.46s each; build86.27s0warnings/errors. Exact TRX execution instants: Core2026-09-08T02:24:14.9281432Z→02:24:17.1062787Z (247/247); Integration02:24:18.7911799Z→02:25:21.9517200Z (28/28). Both0failed/skip/error/inconclusive. The one Top15 test loops15 originals internally, not15 separate xUnitcases. Later proof readback/retention is not another test run.
