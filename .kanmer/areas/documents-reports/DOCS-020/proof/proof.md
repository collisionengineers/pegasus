---
kind: proof-record
merged_sha: "4d7ad4a0d2593300fd02527838aa2f1cf6555860"
environment: "Windows PowerShell7, .NET10 Release, existing SQL fixtures; .worktrees/verify-docs-020-4d7ad4a0d2593300fd02527838aa2f1cf6555860"
verified_at: "2026-09-07T23:12:31Z"
result: PASS
attempts:
  - attempted_at: "2026-09-07"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
    exit_code: 0
    result: PASS
    summary: "Fresh exact-merge locked restore passed."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
    exit_code: 0
    result: PASS
    summary: "Fresh exact-merge solution build passed, zero warnings/errors, 139.38 seconds."
  - attempted_at: "2026-09-07"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss|FullyQualifiedName~CaseReportGenerationPersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.LatestMigrationGivesFoundationTablesTheirExactRuntimePermissions|FullyQualifiedName~CaseArtifactCustodyRecoveryTests\" --logger \"trx;LogFileName=docs-020-merge-attempt1.trx\""
    cwd: ".worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
    exit_code: 1
    result: FAIL
    failure_kind: implementation
    summary: "53 total: 52 passed, one failed, zero skipped, 134 seconds. Existing exact pending-migration expectation omits DOCS-020's new permission migration."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/docs-020"
    exit_code: 0
    result: "PASS"
    summary: "Premerge test-only correction build: zero warnings/errors, 47.26s; exact start instant not captured."
  - attempted_at: "2026-09-07T22:45:44.1993952Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss\" --logger \"trx;LogFileName=docs-020-correction.trx\""
    cwd: ".worktrees/docs-020"
    exit_code: 0
    result: "PASS"
    summary: "Premerge corrected consumer: 1/1 passed, zero skipped, reported test duration21s; TRX finish22:46:07.8142460Z. Not relabelled as merged evidence."
  - attempted_at: "2026-09-07"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-docs-020-4d7ad4a0d2593300fd02527838aa2f1cf6555860"
    exit_code: 0
    result: "PASS"
    summary: "Root locked restore at exact follow-up merge4d7ad4a0d2593300fd02527838aa2f1cf6555860; exact start instant not captured."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-docs-020-4d7ad4a0d2593300fd02527838aa2f1cf6555860"
    exit_code: 0
    result: "PASS"
    summary: "Root exact follow-up merged integration-project Release build: zero warnings/errors, 58.76s; exact start instant not captured."
  - attempted_at: "2026-09-07T22:59:22.6407323Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss\" --logger \"trx;LogFileName=docs-020-merge-correction.trx\""
    cwd: ".worktrees/verify-docs-020-4d7ad4a0d2593300fd02527838aa2f1cf6555860"
    exit_code: 0
    result: "PASS"
    summary: "Root exact follow-up merged correction: 1/1 passed, zero skipped, reported test duration28s; TRX finish22:59:52.6459421Z. Original52 passing cases reused only with immutable-input comparison."
---

# DOCS-020 final integrated proof

## Outcome and authority

PASS for the corrected DOCS-020 integration at
4d7ad4a0d2593300fd02527838aa2f1cf6555860 (PR685, merged22:53:39Z).
This combines the original exact-merge52 passes with the corrected consumer's
fresh exact-follow-up merged PASS; it is not a new53-case run at4d7.
Root owns every executable check. principal_delivery_audit assembled the
proof and independently read the retained results and source comparisons.
No Done, cleanup or deployment action is authorized by this proof alone;
root must read this whole record first.

Original PR682 merged522e67f270ab4d6086d9fba04095988db3598888 from
reviewed1bf9ac613a2b7610d2ddc23e8acfd9f4b462ef79, reviewfdf4270802857212.
The original proof50597d092c61d794 was FAIL, not a transient failure:
the exact pending-migration test omitted the actual
20260907210000_ReportInputInvalidationPermissions migration. Its three
attempts are preserved above without changing the historical FAIL.
Follow-up PR685 changed only that assertion (+2/-1), preserving the
historical CaseSignOffEngineer target and all custody identity, ordinal and
table assertions. Independent review4db9227104d0a60e/public5135623992
binds corrected head2b1d700ba70336560934b171fa623d8c66df5559.
Plan6418f2e4c8f57b8f and report6a6c20140f7a7537 retain both phases.

## Exact merged source and justified reuse

GitHub PR685 was read as MERGED with the full SHA above. The named worktree
resolves to itself under .worktrees, shares only the source repository's
common Git directory, is detached (symbolic-ref exit1/empty), clean and
exactly at that SHA (all assertion/inspection commands exit0). It is
neither the board nor author worktree. Merge ancestry to origin/dev exit0.

The correction file is byte-identical between reviewed head2b1d700... and
merge4d7ad4a... (git diff --exit-code exit0). Full trees are NOT equal:
the merge incorporates four INTK-062 public-upload paths. Full change census
from the previously tested522e67f merge to4d7ad4a is exactly:

- docs/frd/frd-02-intake-and-source-identity.md
- src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs
- src/Pegasus.Web/Pages/Uploads/RequestUploadTransportFilter.cs
- tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs
- tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs

The first four only affect the public-upload route/transport cohort, not
report generation, freeze, source/signatory mutation, runtime roles or
custody recovery. The fifth corrects the failed expected migration list
without changing production behavior. Core/Infrastructure, report tests,
runtime-role/custody-recovery tests, shared IntakeWebTestSupport and
integration-project inputs compare unchanged with exit0. This is the
mechanism and immutable-input basis for root's authorized reuse of52 passing
cases, not a full-tree or binary-equivalence claim. Both comparisons were
rechecked while assembling this final proof; detailed original comparisons
remain in scratch/verify65c9521d6f531dbe.

## Actual retained runtime evidence

All three TRX records were read in full for counters/failed test identity and
hashed independently. No skipped, aborted or inconclusive case is counted PASS.

| TRX | Exact source | Result | UTC start → finish | SHA256 |
| --- | --- | --- | --- | --- |
| docs-020-merge-attempt1.trx | 522e67f270ab4d6086d9fba04095988db3598888 detached | 52 PASS / 1 FAIL / 0 skipped | 22:39:46.8406513 → 22:42:03.6990949 | 6E9BE170FB9F1BD55CABEAE831B09947C9901A454A3CB91FABE934D58AEF849E |
| docs-020-correction.trx | author correction, before PR685 merge | 1 PASS / 0 skipped | 22:45:44.1993952 → 22:46:07.8142460 | 9E82F4D831360216231988E50EA64DE0EEC6C79F16C1AD267F0131A0B6425637 |
| docs-020-merge-correction.trx | 4d7ad4a0d2593300fd02527838aa2f1cf6555860 detached | 1 PASS / 0 skipped | 22:59:22.6407323 → 22:59:52.6459421 | 3B61CFA616681C35E826D32255A0B875F89A757557A189FE4FDB3BD7129E5BA3 |

Dates are2026-09-07 UTC. Logs live beneath tests/Pegasus.IntegrationTests/
TestResults in the respective workspace from the attempt records. TRX
envelope duration includes runner overhead, so it differs from reported
test-duration134s/21s/28s. Restore/build attempt start instants were not
captured and remain honestly date-precision.

The52 passes include report snapshot/read/freeze races, source and signatory
invalidation, stale preparation refusal, exact generated-output exclusion,
actual runtime-role callers, artifact recovery and London preview dates.
The new merged1-case PASS closes the sole failed migration consumer.
No application or schema fix was needed after the original merged run.

## Earlier attempts retained, not erased

The full premerge chronology remains in report6a6c20140f7a7537
(and original08e9663b3940a805):

- Initial Release build FAIL exit1, EF1002 after56.31s; parameterized role
  assignment fixed that test setup. Corrected build PASS18.20s.
- Broad focused148-case attempt FAIL exit1:143 passed,5 failed,0 skipped,
  7m51s; those five lacked IDocumentContentStore harness composition.
  Existing local composition fixed the harness; assertions were preserved.
- Snapshot update initially failed1/2 because case-details--unavailable
  was not captured. No production defect or fabricated snapshot pass.
- Final incremental build PASS45.74s0warnings/errors, corrected five plus
  the missing Razor capture PASS6/6,52s. Snapshot update2PASS538ms,
  verification2PASS10s, cataloguePASS62routes/69prototypes/0brokenrefs,
  migration-grant checkPASS101migrations. Normalized three captured report
  snapshots/index were unchanged tracked bytes; no manual visual pass.
- A diagnostic core.autocrlf=false whitespace check produced exit1; normal
  repository-configured diff --check passed. No Git setting/file was changed.
- After original merged FAIL, merge --ff-only refused before mutation;
  ordinary history-preserving dev merge succeeded. No reset/rebase or
  suppression of the failed consumer. The new follow-up is test-only.

These are premerge evidence and process history, not falsely repeated or
relabelled exact4d7 runtime checks. Current report callers, generated UI
bytes and grants are unchanged at the final merge.

## Limits and handoff

Ordinary acceptance is integrated on configured dev, not deployed on main.
No cloud/provider/email write, manual viewport review, fresh full solution
test rail or live report delivery is claimed. Final integrated release CI
remains root-owned; these focused checks do not claim to replace a newly
required check. Root explicitly approved targeted verification and unchanged
evidence reuse to avoid duplicate whole suites.

This whole PASS record supersedes, but does not erase, FAIL50597d092c61d794.
Kanmer-verify required exact merged source binding, preserved failure
history and honest evidence reuse. Root whole-proof read is next; retain
both detached worktrees, author branch/worktree, claims and all TRX records
until separate Done/closeout authority.
