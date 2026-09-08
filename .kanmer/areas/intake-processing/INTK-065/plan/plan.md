# Plan — INTK-065: Current principal evidence source inventory

## Objective

Repair the current-source inventory drift exposed by PR700 without changing
runtime behavior, immutable originals, or the historical v1 review dossier.

## Starting state

Evidence: research/research.md@6971e022bb4053b6; files/files.md@18ba3fcb9b7aec2b.
Read-only accepted dev: 96777888bfa7ee7f85d63979a4a09ae10cda7d13.
PR700 head f86054c0e7cc73cb6245355dd21c03e58196d582 inherits this defect;
it does not change the affected files. Twelve Pegasus snapshots were checked:
three deleted paths, one changed contract hash, eight exact hash/size matches.
The exact QDOS extraction source nevertheless declares v8 while its ID says v7.

Bounded sibling/workspace/environment checks found no complete historical
CollisionSpike source root or email cohort. Full original-input regeneration
is unavailable on this host, not PASS. This plan proposes existing-helper
regeneration of the affected source inventory only. Root must approve this
evidence boundary and a narrow four-path handoff before execution.

INTK-060 remains foreign-taken/Verifying. Historical exact ownership is
A/Foundation for generator and README, Closed for JSON, C/Domain for tests.
Preserve those claims/history. Root owns the supplemental EPIC-014 run;
the frozen 218-ticket roster does not change.

## Governing docs

Meets docs/frd/frd-09-provider-and-intermediary-routes.md: current Core/FRD-09
own the fifteen supported profiles and fail-closed activation, while reference
evidence is not runtime configuration. No FRD or ADR change is needed.
The existing README and generated purpose prose explicitly distinguish the
historical v1 review states/evaluation from current source links. Neither
turns historical results into proof of today's runtime.

## Required changes

Correct these five existing source records using their current files:

| Source ID | Existing current owner |
| --- | --- |
| principal-case-match-v1 | PrincipalCaseMatchPolicy |
| principal-mail-route-v1 | PrincipalMailRoutePolicy |
| principal-mail-classification-v1 | PrincipalMailClassificationPolicy |
| qdos-extraction-policy-v8 | QdosInstructionExtractionPolicy |
| shared-mail-taxonomy (unchanged ID) | MailClassificationContracts |

The first three replace deleted QDOS policy IDs/paths; QDOS extraction changes
only ID; shared taxonomy changes hash/byte count. Update every corresponding
generator and package evidenceRefs reference atomically. Keep historical
qdos-policy-v5-volume-evaluation and its policy key/version unchanged.

Reuse snapshot(), canonical_json_bytes(), publish() and the existing hash
modes. Lift only the existing five-row policy inventory literal to one
module-level constant consumed by the existing full generator and the bounded
one-time mechanical refresh. This is the same inventory, not a second list,
new CLI mode or permanent refresh tool. The one-time refresh loads the retained
JSON, regenerates those five records through snapshot(), applies only the
four explicit ID replacements to references, updates purpose prose and uses
the existing canonical serializer. Record its exact invocation/output in the
report. Do not attempt to synthesize absent historical inputs.

Update the existing Core test's ID expectations and retain all assertions.
Add current policy-version/ID, source existence and normalized byte-count
checks alongside the existing SHA-256 and evidence-reference checks. No
exclusions, old-path aliases, dropped snapshots or reduced corpus coverage.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `scripts/reference_data/build_principal_identification_corpus.py` | Existing inventory/IDs and historical-purpose clarification |
| Modify | `reference/workproviders-and-repairers/principal-identification-corpus.v1.json` | Generated current-source metadata and reference changes only |
| Modify | `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs` | Preserve coverage; current IDs, bytes, hashes and diagnostics |
| Modify | `docs/principal-rules-and-mappings/README.md` | Historical baseline versus current activation/source-link distinction |

## Do not modify

- `src/**`
- `corpus/**`
- `pegasus_pack/**`
- `workspaces/**`
- `.github/**`
- `docs/operator-notes.md`
- `scripts/Build-PrincipalIdentificationCorpus.ps1`

All other original/reference files, historical evaluations, unrelated tickets,
claims and the original roster are outside the four-file write map.

## Constraints

No runtime change or production deployment claim. The existing generator CLI
and Core corpus test are the actual consumers. No new dependencies, schema,
pipeline, policy grammar, source copies, activation states or refresh framework.
PowerShell 7 on Windows for this host. Root owns all builds/tests; author does
not launch a competing verification session.

## Ordered steps

### Step 1 — Correct the existing source inventory and consumers

- Preconditions: root approves the complete plan, exact path handoff and supplemental run; fresh packet/ownership checks permit the recorded isolated ticket worktree.
- Files: `scripts/reference_data/build_principal_identification_corpus.py`, `reference/workproviders-and-repairers/principal-identification-corpus.v1.json`, `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs`, `docs/principal-rules-and-mappings/README.md`.
- Change: apply the five-record/four-ID correction and bounded historical-purpose clarification above; strengthen existing source assertions.
- Preserved behavior: every historical original hash, evaluation count, cohort/group, criterion state and crosswalk; all existing Core assertions.
- Forbidden: restore old policies, rewrite v5 results, promote review candidates, fabricate originals or introduce another generator.
- Negative cases: stale/missing paths, wrong policy ID/version, bytes/hash mismatch and dangling evidence references must still fail clearly.
- Tests: existing PrincipalIdentificationCorpusTests and the existing Python hash-mode tests.
- Done when: only the four mapped files differ and source is frozen for root verification.
- Deviation stop: any unexpected historical-object difference, changed runtime source or unresolved ownership.

### Step 2 — Prove the inventory-only correction

- Preconditions: Step 1 source is frozen and root grants the sole verification slot.
- Files: `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs`.
- Change: verification only; no planned source edit.
- Preserved behavior: initial CI failure and every later failed attempt remain in the report.
- Negative cases: unavailable full original-input regeneration is explicitly not PASS; no skipped original cohort is presented as executed.
- Tests: focused Core corpus class, existing two Python hash-mode tests, documentation links, deterministic inventory/unchanged-data comparison.
- Done when: root supplies actual exit evidence for the checks below and author independently reads it; report/checklist are complete for independent review.
- Deviation stop: any failing check; report and obtain a bounded correction before rerunning.

## Acceptance checks

1. All twelve current Pegasus paths resolve with their declared hash mode,
   normalized/raw byte count and SHA-256; four source IDs match current policy
   versions. Every criterion evidenceRef still resolves.
2. Compare base and resulting JSON structurally using the existing canonical
   serializer: evaluationSummaries, evidenceItems, historicalCrosswalks,
   supportingIdentities, coverage/cohorts, sharedTaxonomy and criterion states
   are unchanged. Principals differ only by the four declared source-ID
   reference substitutions. Other package content is unchanged except the
   declared purpose clarification and five inventory records.
3. Seven data/reference snapshots are identical. QDOS extraction bytes/hash
   are identical. Current affected snapshots are reproduced deterministically
   from the same inputs and canonical package bytes compare equal on the
   second in-memory generation. Record before/after hashes and exact diff.
4. Preserve the 49-principal, 40-active/9-dormant historical census, six accepted
   QDOS predicates, eight observed candidates and original v5 evaluation
   assertions. These remain historical statements, not current activation.
5. Full Build-PrincipalIdentificationCorpus generation/-Verify from original
   roots is unavailable and is not claimed. No historical evaluation rerun,
   original-reader corpus cohort, UI capture or live provider operation is
   required for this inventory-only fix.

## Commands

Cwd is the eventual recorded isolated ticket worktree. Root owns all runtime
checks and the required locked restore/Release build; no author build/test.

```powershell
git diff --check
python -m unittest discover -s scripts/reference_data/tests -p test_build_principal_identification_corpus.py
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PrincipalIdentificationCorpusTests" --logger "trx;LogFileName=intk-065-source-inventory.trx" --results-directory ./artifacts/verification
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
```

The mechanical regeneration/check invocation using the named existing helpers
is recorded verbatim with outputs in the report; it does not add a repository
command or replace the existing full-input wrapper. Root coordinates any
required final CI, not repeated speculative runs.

## Failure and deviation rules

Stop on unexpected scope, data diff, ownership conflict, missing current source,
failing assertion or unavailable mandatory proof. Do not weaken tests to make
CI pass. No force, foreign-claim release, original modification or cloud write.
A later pass preserves the earlier failure.

## Stop condition

Current assignment stops Preparing and untaken with complete documents for
root review. After explicit execution authorization, stop at independently
reviewable PR/Review with truthful root evidence; no self-review, merge, Done,
cleanup or next ticket. Exact-merge verification/closeout remain later phases.
