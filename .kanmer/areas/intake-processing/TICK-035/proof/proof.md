---
kind: proof-record
merged_sha: "56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
environment: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7; Windows x64; PowerShell 7"
verified_at: "2026-09-08T02:33:10Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T02:22:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root86956; approximate issued minute only, precise start not captured. Locked restore7 projects PASS; each at most1.46s."
  - attempted_at: "2026-09-08T02:22:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root86956; approximate script-issued minute, not exact build start. Full Release build86.27s, zero warnings/errors."
  - attempted_at: "2026-09-08T02:24:14.9281432Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ProcessIntakeTests|FullyQualifiedName~MailRoutePolicyTests|FullyQualifiedName~MailClassificationPolicyTests|FullyQualifiedName~CaseMatchPolicyTests|FullyQualifiedName~EvaluateIntakeCaseMatchTests|FullyQualifiedName~InstructionExtractionPolicySelectorTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests|FullyQualifiedName~ProviderInstructionPolicyTests' --logger 'trx;LogFileName=tick-035-merged-core.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root86956;247 executed/passed,0failed/error/skipped/inconclusive; reported420ms test duration. Exact TRX interval retained below."
  - attempted_at: "2026-09-08T02:24:18.7911799Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseMatchIntegrationTests|FullyQualifiedName~InlineForwardedMailRouteTests|FullyQualifiedName~QdosAllocationRecoveryTests.GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions|FullyQualifiedName~QdosAllocationRecoveryTests.ClassificationNegativeAndAmbiguityFixturesPersistWithoutInventedCaseTypes|FullyQualifiedName~QdosAllocationRecoveryTests.PersistedStaffForwardRetainsOuterTransportAndOriginalQdosIdentity|FullyQualifiedName~CaseDataCompletenessPersistenceTests.IntakeFieldCandidatesRetainProvenanceAcrossReceiptPersistence|FullyQualifiedName~ProviderApiSubmissionTests.ASubmissionMatchingAnExistingCaseIsRejectedWithoutMutationOrDuplicateAllocation|FullyQualifiedName~Top15InstructionCorpusTests.OneGenuineInstructionPerPrincipalProvesSelectedWorkTypeAndMatchKeys' --logger 'trx;LogFileName=tick-035-merged-callers.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root86956;28 executed/passed,0failed/error/skipped/inconclusive; reported61s test duration. Includes1 xUnit test internally iterating15 genuine originals."
  - attempted_at: "2026-09-08T02:25:21Z"
    command: "git diff --check"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root post-test check PASS; approximate completion minute, exact instant not captured; no source/whitespace diff."
  - attempted_at: "2026-09-08T02:25:21Z"
    command: "git status --short --branch"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root post-test check PASS; approximate completion minute; clean detached exact merge."
  - attempted_at: "2026-09-08T02:25:21Z"
    command: "git rev-parse HEAD"
    cwd: ".worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7"
    exit_code: 0
    result: PASS
    summary: "Root post-test check PASS; approximate completion minute; HEAD exactly56566371a5b80ef59c4f98e377c8e8ff6469b5f7."
---

# TICK-035 exact merged verification

PASS for [PR692](https://github.com/collisionengineers/pegasus/pull/692),
merged into the configured integration branch dev at2026-09-08T02:20:31Z:
56566371a5b80ef59c4f98e377c8e8ff6469b5f7.

Root executed every restore/build/test and post-check command above in its
single guarded job86956, exit0. pack_reconcile prepared this proof from root's
completed command record and independently read actual TRXs, hashes, source
identity and retained evidence. This record does not claim a second execution,
author self-review, whole repository test suite, hosted CI, live mail/provider
call or deployment.

## Authority, worktree and time binding

Whole current plan7a28b8ab58ed1ca2, filesa37303dd2d9f8f1f,
report70312df54319f367, checklist54a984eff71f67c8 and independent
reviewdf2745313906828d have been read. Fresh gates find Verifying with no
unresolved questions; root owns the stage decision after reading this proof.
The independent delta review binds authored
cca5521a6315420129320061759209273bb64c67 and carries F-001/F-002/F-003
fixed dispositions, original needs-changes29da3f33c888cb13 and preflight
7074751553d263d8 without erasing their history.

GitHub freshly confirms MERGED and the exact full SHA above. Root created
the deterministic detached verifier root; readback confirms it is detached,
clean and exactly56566371a, with the same repository common .git directory.
It is neither .worktrees/tick-035 nor the board/shared checkout. No mutable
checkout was pulled, reset, switched or repaired.

Actual reviewed-to-merge diff is only the seven previously accepted ENG-041
correction files, +210/-71. The full merged build and intake/Case callers
below run against their integrated source; this is not a claim that every
ENG-041-specific test was rerun. TICK-035's runtime implementation is unchanged
after its reviewed guard correction; no full-tree author/merge equality is
claimed. Existing Settings source, OrganizationDirectoryWebTests and
docs/design/test-ui snapshots compare identical between author and merge
(exit0). This supports only the source-scoped UI evidence reuse below.

Root's complete exact command strings and attribution are in
scratch/verify3e7f94faf5c2b62d. PEGASUS_REFERENCE_PACK_ROOT was injected as
the existing ignored source-repository pegasus_pack path; source samples were
not copied into tracked files. Capture directory/scope/mode environment
entries were removed for this process: no UI capture ran.

Restore/build were issued during the observed02:22UTC minute, but precise
start instants were not separately captured. Frontmatter timestamps for
those two commands identify that approximate script-issued minute, not
invented exact starts. Post-Git check timestamps similarly identify the
observed post-test completion minute. Core/Integration timestamps are exact
TRX values, converted from the recorded+01:00 local offset.

## Exact command outcomes and actual callers

Locked restore: all7 projects PASS, each at most1.46s.
Whole Release build: PASS86.27s,0 warnings/errors.
Core:247 executed/passed,0 failures/errors/skips/inconclusive; reported420ms.
Integration:28 executed/passed,0 failures/errors/skips/inconclusive;
reported61s. Reported test durations differ from the enclosing TRX intervals.

Core class census from the actual247 results:

| Existing class | Passed |
| --- | --- |
| PrincipalMailClassificationPolicyTests | 53 |
| InstructionExtractionPolicySelectorTests | 22 |
| ProcessIntakeTests | 48 |
| EvaluateIntakeCaseMatchTests | 31 |
| PrincipalCaseMatchPolicyTests | 29 |
| ProviderInstructionPolicyTests | 8 |
| PrincipalMailRoutePolicyTests | 51 |
| DefinitiveIntakeCaseTypeTests | 5 |

Integration census from the actual28 results:

| Existing caller cohort | Passed |
| --- | --- |
| CaseMatchIntegrationTests | 8 |
| InlineForwardedMailRouteTests | 10 |
| GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions (ALS/YML/FW/SBL) | 4 |
| ClassificationNegativeAndAmbiguityFixturesPersistWithoutInventedCaseTypes | 1 |
| PersistedStaffForwardRetainsOuterTransportAndOriginalQdosIdentity | 1 |
| IntakeFieldCandidatesRetainProvenanceAcrossReceiptPersistence (located true/false) | 2 |
| ASubmissionMatchingAnExistingCaseIsRejectedWithoutMutationOrDuplicateAllocation | 1 |
| OneGenuineInstructionPerPrincipalProvesSelectedWorkTypeAndMatchKeys | 1 |

The last row is one xUnit case internally iterating15 genuine originals,
not15 independently reported test cases.

These results exercise the real durable intake chain
ReceiveIntake -> ProcessQueuedIntake -> ProcessIntake -> existing
Case/retained-mail association -> IAllocateIntake, with SQL receipt/index/
source-link persistence and reconstructed replay. CaseMatch callers prove
same-transaction index writes, ordinary staff index changes, provider-scoped
candidate lookup, preserved evaluation evidence, reversible association,
live staff edit-lease independence and stale retained-mail evidence refusal.

ALS/FW/SBL immutable original emails create one actual Inspection Case,
associate a repeat to that same identity and replay without another Case.
Typed facts, source hashes/origin/provenance, image counts and readiness are
asserted. ALS retains column2 locators and rejects missing/duplicate client
cells, misplaced party columns and contradictory same-table-number sources
from separate physical documents. Located/unlocated receipt JSON roundtrips
preserve every candidate field, including Locator and RawValue.

The ALS F-003 probe first proves an actual unique existing-Case target with
usable typed keys, then removes only a required profile signal from the
same original's decoded content. Its explicit structural ReaderKey prevents
it being claimed as a new genuine envelope. Real processing/replay persists
Accepted ALS/NeedsSorting without match/draft/Case/allocation/link and leaves
exactly the earlier one Case. Original source bytes remain unchanged.
QDOS no-competing-profile matching and conflicting-profile refusal remain
covered by the Core cohort; genuine retained/inline forward callers preserve
original sender/current-envelope boundaries. Declared authenticated Provider
API creates once and rejects a subsequent existing-Case submission before
draft/allocation; the mail-profile guard does not govern that route.

YML HD4021 remains a genuine later-report-correspondence negative: accepted
exact mailbox but no current instruction/draft/type/Case and no allocation
after replay. Quoted two-deep history does not become an instruction. The
standalone original YML PDF/profile pass is separate evidence; no missing
initial YML-envelope allocation is invented. MP's unchanged hash-bound scan
must qualify through the real PdfPig reader before supplied hash-bound Astra
OCR text enters the existing mapping. This is not a new Azure OCR response.

## Exact merged TRXs

Both files were read after root reported job86956 complete, with every test
name/counter inspected. Source hashes match root's independently recorded
values and retained copies.

| File under verifier artifacts/verification | SHA-256 |
| --- | --- |
| tick-035-merged-core.trx | 1913B6DEDDB7A5945701CC2652A7BCFB7FBA5B9CF70B82023F2A645B6BCE1449 |
| tick-035-merged-callers.trx | 2BEDD5743E4142EB20994F8A07A4CBEC74189409E6C36593EDA9909373FF6AA9 |

Core interval:2026-09-08T02:24:14.9281432Z to02:24:17.1062787Z.
Integration interval:2026-09-08T02:24:18.7911799Z to02:25:21.9517200Z.
The original TRXs display those same instants with+01:00 offset.

Root's final git diff --check, git status --short --branch and git rev-parse
HEAD each returned0; source remained clean/detached at the exact merged SHA.
pack_reconcile repeated only read-only identity/diff inspection, not runtime
checks. No command on this exact merged verification failed or skipped.
Earlier author/runtime and review failures are preserved next rather than
being mislabelled part of a fresh green rail.

## Prior failed and passing attempts preserved

The whole report70312df54319f367 remains the chronological author command/
diagnosis record. Its earlier pending handoff statements describe earlier
attempts and do not override its final source verification or this merged
proof. The following13 actual author TRXs remain distinct and are archived
with counters, exact timestamps, original paths and SHA-256 in the manifest.

| Author result file | Passed / failed / skipped | Preserved disposition |
| --- | --- | --- |
| tick-035-core.trx | 224 / 12 / 0 | FAIL: obsolete accepted-domain inventory expectations and route version. |
| tick-035-core-correction.trx | 14 / 0 / 0 | PASS:12 failures plus2 preserved intermediary negatives. |
| tick-035-integration.trx | 21 / 2 / 0 | FAIL: MP original needed OCR and ALS Case-field provenance join. |
| tick-035-provenance-core.trx | 9 / 0 / 0 | PASS: canonical field binding9. |
| tick-035-provenance-integration.trx | 3 / 2 / 0 | FAIL: rotated MP coverage and ALS expected readiness. |
| tick-035-combined-core.trx | 53 / 0 / 0 | PASS: shared classifier53. |
| tick-035-combined-integration.trx | 12 / 2 / 0 | FAIL: YML closing boundary and hash representation expectation. |
| tick-035-yml-hash-settings.trx | 4 / 1 / 0 | FAIL: genuine YML later correspondence incorrectly expected as instruction; other4 passed. |
| tick-035-genuine-four.trx | 3 / 1 / 0 | FAIL: ALS persisted Locator; FW/SBL/YML negative passed. |
| tick-035-table-identity.trx | 10 / 0 / 0 | PASS: physical-document structured binding10. |
| tick-035-persisted-provenance.trx | 3 / 0 / 0 | PASS: genuine ALS plus2 JSON roundtrips (3 cases, not5). |
| tick-035-profile-guard-core.trx | 2 / 0 / 0 | PASS: preserved QDOS fallback and cross-profile refusal2. |
| tick-035-profile-guard-destinations.trx | 2 / 0 / 0 | PASS: F003 real ALS target/refusal/replay and actual API2. |

This preserves all six failed author TRXs, including the original Core12
failures and repeated genuine-source failures. A later PASS does not erase
them or turn an earlier partial cohort into a complete PASS. The final
pre-provenance correction was exactly3 Integration cases, not5.
F-001/F-002 were fixed at7fcd4c662c5457024d1d20c5fe0c02c842e98a63;
F-003 atcca5521a6315420129320061759209273bb64c67. No remaining finding is
silently discarded. Each reviewed remedy uses its existing owner; no parser
failure was broadly converted to success or to a new service.

The report also retains every earlier successful restore/build with its
durations, and bounded research/command-input failures that were not test
executions. Root's pre-run lookup of three guessed test paths returnedexit1;
rg --files resolved the real paths before the guarded merged run. Those
lookup failures are not runtime PASS, but supplied no failing application
evidence. No replacement log or exact unrecorded start time was fabricated.

All ten TRX filenames explicitly named by the report exist; the three
additional initial Core/pure provenance result files also exist. No named
TRX is missing or proven overwritten. All five report-published artifact
hashes match. Earlier hashes not printed in that report are newly measured
retention evidence, not retroactive original attestations.

## Scoped Settings evidence reuse and honest limits

The earlier root Settings run retained actual QDOS/YML Settings plus the
Administration capture, scoped snapshot update2 PASS, verify2 PASS and
catalogue60 routes/67 prototypes/0 broken references. The current source and
committed snapshot equality check is the reason that exact source-bound
evidence can be reused; no new Settings capture, final-merge snapshot run,
manual visual pass or all-principal visual review is claimed.

The author capture directory holds40 files/20 complete HTML/metadata pairs
(596524 bytes). Each directory's request-plus-HTML identity was independently
recomputed and matched; its write-once owner preserves different HTML hashes.
The snapshot script did not specify a TRX logger, so no dedicated author
snapshot-update/verify TRX exists. Those outcomes remain root command/report
evidence, not a manufactured recovered TRX or a claim of proven overwriting.

Fresh GitHub read confirms dev unprotected, active rules[], required check
contexts[]/checks[] and reviewed-head check_runs[]. Empty/nonrequired check
lists are not CI PASS. Root-authorized skip-ci avoided duplicate speculative
runs; final converged release checks belong to the root controller and are
not inferred from this ticket's ordinary integration acceptance. No Box/
mailbox/provider/cloud write, live Azure operation or deployment occurred.

## Durable local retention before any cleanup

Root authorized artifact retention after completion. All15 TRXs
(13 author+2 merged) and all40 author capture files were copied to ignored
pegasus_pack/current/proofs/TICK-035 and source/destination SHA-256 values
verified,55 files total, with no overwrite of different existing evidence.
Full original-to-retained path mapping/counters/times/hashes are in
pegasus_pack/current/proofs/TICK-035/manifest.json.

- Author results and captures retain their original subpaths beneath author/.
- Exact merged results retain artifacts/verification beneath merged-56566371/.
- The earlier read-only inventory is
  pegasus_pack/current/tick-035-closeout-inventory.md and its JSON companion.

Original command working directories above are historical execution locations;
the archive locations remain valid after a later authorized closeout.
Neither worktree, branch nor claim has been removed or released here.
Root retains Verifying lease30; there is no stage or lease mutation by this
proof writer. Only after root reads this whole PASS, refreshes gates and
moves Done may separately authorized normal closeout proceed. Outcome is
integrated dev acceptance, not deployment.


## Closeout preparation after verified Done — 2026-09-08

Root read the full original proofa4cb5906f9bfa2c7, refreshed synchronized board
and gates, and moved Done at2026-09-08T02:34:38.664Z. The previous final
paragraph's Verifying/lease30 description is its historical proof-writing
state; the accepted verdict/attempts/hashes remain unchanged.

Fresh complete include-archived census finds only TICK-035 occupying the
recorded TICK-035-principal-routes branch and .worktrees/tick-035, no batch.
The separately approved verifier root is
.worktrees/verify-tick-035-56566371a5b80ef59c4f98e377c8e8ff6469b5f7.
Both exact paths resolve within the intended repository .worktrees directory,
share C:/Users/Alex/Documents/GitHub/pegasus/.git, and have clean tracked/
untracked Git status. Author HEAD iscca5521a6315420129320061759209273bb64c67;
verifier is detached at56566371a5b80ef59c4f98e377c8e8ff6469b5f7.
No other ticket/root/claim is authorized for cleanup.

Fresh GitHub read confirms PR692 MERGED at2026-09-08T02:20:31Z with that
full merge SHA. After fresh origin/dev fetch, git merge-base --is-ancestor
56566371a5b80ef59c4f98e377c8e8ff6469b5f7 origin/dev passes exit0.
Ticket commit/delivery traceability now names this reachable merged commit,
not an unmerged author tip; original author history stays in report/review.

Before any removal, the archive manifest's SHA256 was rechecked as
011233769A658F032BA56E5EDA3EC38DBCB429513978839B52851F454D7BA5BC.
Every one of15 TRXs and40 capture files matched its expected SHA256 both at
the source and retained destination (55/55). All13 author results, including
all six failures, the two merged PASS TRXs and all20 complete capture pairs
remain preserved. No artifact was overwritten, recreated as a fake prior run
or removed from the archive.

No source/build/test, deployment/cloud/mail action, stage move or lease change
was performed during this record-keeping. Normal Git cleanup and release-last
will be recorded separately below; no force operation is authorized.
