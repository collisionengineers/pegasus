# Plan — INTK-065: Current principal evidence source inventory

## Objective

Repair current principal-source inventory drift and move the two principal
profile documents to their approved location, without changing runtime policy,
immutable originals, or the historical v1 review dossier.

## Starting state

Evidence: research/research.md@4807bf2fa58c664d; files/files.md@fa373139387e7e7f.
Approved base is origin/dev `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`. Three obsolete QDOS policy
paths were deleted, the shared classification contract bytes changed, and the
unchanged extraction source is version 8 although its prior inventory ID was
v7. Full original-input regeneration is unavailable on this host and is not
PASS.

Root approved a narrow supplemental EPIC-014 handoff: phase 1 may edit the
generator and Core test and perform the exact byte-identical documentation
relocation plus index repair. The generated package and every command remain
for the root-held verifier slot. INTK-060 and historical A/Foundation, Closed,
and C/Domain records remain untouched.

## Governing docs

Meets `docs/frd/frd-09-provider-and-intermediary-routes.md`: Core/FRD-09 own
current supported profiles and reference evidence is not runtime configuration.
No FRD or ADR change is needed. The document move is organizational only; it
does not revise the historical QDOS review baseline or claim it proves current
activation.

## Required changes

Correct the existing generator/test inventory for:

| Source ID | Current owner |
| --- | --- |
| principal-case-match-v1 | PrincipalCaseMatchPolicy |
| principal-mail-route-v1 | PrincipalMailRoutePolicy |
| principal-mail-classification-v1 | PrincipalMailClassificationPolicy |
| qdos-extraction-policy-v8 | QdosInstructionExtractionPolicy |
| shared-mail-taxonomy | MailClassificationContracts |

The first three replace deleted QDOS paths; extraction changes only its ID;
shared taxonomy changes only bytes/hash. Preserve all historical v5 evaluation
and evidence objects. Relocate README.md and qdos.md exactly, then repair only
the index target. Use the existing helper for the tracked JSON only after root
grants the verifier slot; add no CLI, refresh framework, source copy, or
compatibility alias.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `scripts/reference_data/build_principal_identification_corpus.py` | Current-source declaration corrections only; no execution in phase 1. |
| Modify | `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs` | Matching expectations and diagnostics; no test run in phase 1. |
| Rename from | `docs/principal-rules-and-mappings/README.md` | Byte-identical source. |
| Rename from | `docs/principal-rules-and-mappings/qdos.md` | Byte-identical source. |
| Rename to | `docs/principal-profiles/README.md` | Byte-identical destination. |
| Rename to | `docs/principal-profiles/qdos.md` | Byte-identical destination. |
| Modify | `docs/index.md` | Replace only the moved README target. |
| Modify (verifier slot only) | `reference/workproviders-and-repairers/principal-identification-corpus.v1.json` | Existing-helper output only. |

## Do not modify

- `src/**`
- `corpus/**`
- `pegasus_pack/**`
- `workspaces/**`
- `.github/**`
- `docs/operator-notes.md`
- `scripts/Build-PrincipalIdentificationCorpus.ps1`
- any original, historical-evaluation, ticket, claim, or cloud resource

## Constraints

No runtime or deployment change. No new dependencies, schema, policy grammar,
pipeline, source copy, or tool. Use PowerShell 7 on this Windows host. Root is
the only heavy-verification owner: phase 1 runs no scripts/tests and does not
edit/regenerate JSON. An unexpected historical-object difference, scope
expansion, source change, ownership conflict, or missing current file is a
deviation stop.

## Ordered steps

### Step 1 — Update inventory declarations and relocate principal profiles

- Preconditions: fresh ticket worktree from the approved dev base and the recorded narrow handoff.
- Files: `scripts/reference_data/build_principal_identification_corpus.py`, `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs`, `docs/principal-rules-and-mappings/README.md`, `docs/principal-rules-and-mappings/qdos.md`, `docs/principal-profiles/README.md`, `docs/principal-profiles/qdos.md`, `docs/index.md`.
- Change: correct the five source declarations/expectations; rename the two docs byte-identically; change only the matching index link.
- Preserved behavior: all existing test assertions, historical package objects, evaluation/cohort data, runtime source, and original inputs.
- Forbidden: script/test execution, JSON generation/edit, historical-document rewrite, policy alias, or unapproved path.
- Commands: none; phase 1 explicitly runs no project script or test command.
- Negative cases: a stale/missing source declaration, wrong policy ID/version, or changed rename bytes is recorded as a failure, never fixed by an alias or rewritten history.
- Expected output: seven declared phase-1 paths only; each moved document has identical bytes at its new path; the index has one target replacement.
- Tests: none; reserved for Step 2.
- Done when: the seven phase-1 paths are the only worktree changes and byte equality of each documentation rename has been established.
- Deviation stop: any content change in a relocated document other than its path, any unexpected index diff, or any package/runtime change.

### Step 2 — Materialize and verify under root-held slot

- Preconditions: root expressly grants its verifier slot after reading the frozen phase-1 diff.
- Files: `reference/workproviders-and-repairers/principal-identification-corpus.v1.json`.
- Change: execute the existing helper to materialize only the approved generated inventory correction; do not manually substitute or recreate inputs.
- Preserved behavior: historical hashes, evaluation summaries, evidence items, groups/cohorts, crosswalks, criterion states, and the unchanged source snapshots.
- Forbidden: a new command/tool, full-original regeneration claim, source copy, or broader JSON rewrite.
- Commands: the existing helper invocation, then only the recorded Python, Core, documentation-link, and deterministic/structural comparison commands.
- Negative cases: missing original inputs are INCONCLUSIVE rather than a substitute regeneration; stale path/hash/ID, dangling evidence reference, or unexpected historical change must fail.
- Expected output: the single declared JSON file changes only in approved current-source metadata/references and purpose text, with root-recorded command exits.
- Tests: existing Python hash-mode tests, focused PrincipalIdentificationCorpusTests, documentation link check, and deterministic/structural package comparison.
- Done when: root records truthful exits and the JSON diff is limited to current-source metadata/references and approved purpose text.
- Deviation stop: test failure, unavailable mandatory evidence, unexpected package diff, or missing verifier authorization.

### Step 3 — Commit and hand off

- Preconditions: Step 2 is complete with recorded evidence.
- Files: all Expected files, with no additional path.
- Change: record the implementation report, commit the bounded result, push the ticket branch, and open the one draft PR.
- Preserved behavior: no self-review, merge, verification proof, closeout, or deployment claim.
- Commands: `git diff --check`, commit, push, and draft-PR creation.
- Negative cases: any undeclared path, failed diff check, or missing Step 2 evidence stops the handoff.
- Expected output: one bounded commit and draft PR targeting dev, with the ticket's post-implementation report.
- Tests: `git diff --check`; reuse the Step 2 results.
- Done when: the ticket is in Review with its draft PR and report.
- Deviation stop: any untracked/undeclared path or failed check.

## Acceptance checks

1. Manual: the five current source records have correct path, mode, byte count,
   hash, and policy/version IDs; every relevant evidence reference resolves.
2. Manual: the two relocated documents compare byte-identically old-to-new,
   and the only index modification points to the new README path.
3. Manual: historical evaluation summaries, evidence items, cohorts,
   crosswalks, and criterion states remain unchanged.
4. The verifier slot, if granted, supplies real exits for the named focused
   checks. Full original-input regeneration remains unavailable and is not
   claimed.

## Commands

Phase 1 runs no commands that execute project scripts/tests. In the later
root-held verifier slot only:

```powershell
git diff --check
python -m unittest discover -s scripts/reference_data/tests -p test_build_principal_identification_corpus.py
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PrincipalIdentificationCorpusTests" --logger "trx;LogFileName=intk-065-source-inventory.trx" --results-directory ./artifacts/verification
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
```

Record every invocation and exit; preserve failures. The existing helper
invocation is recorded verbatim in the implementation report.

## Failure and deviation rules

Stop rather than weaken coverage, fabricate inputs, change a foreign claim,
force a lease, or expand scope. A failed attempt remains in the report even if
a later retry passes. Full original-input regeneration unavailable is
INCONCLUSIVE, never a substituted PASS.

## Stop condition

Stop phase 1 with the bounded source/test/document diff frozen for root's
verifier decision. After root grants the verifier slot and Step 2 succeeds,
stop at a draft PR in Review for independent review; do not self-review, merge,
write proof, close out, or start another ticket.
